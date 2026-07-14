using FluentAssertions;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Idempotency.EF;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Messaging.Idempotency.Tests
{
    // US2: a crashed in-flight record recovers after InFlightTtl; a not-applied failure stays retryable.
    public class InProgressRecoveryTests
    {
        private static IdempotencyPurgeHostedService CreatePurge(EfIdempotencyFixture fixture)
        {
            return new IdempotencyPurgeHostedService(
                fixture.Services.GetRequiredService<IServiceScopeFactory>(),
                fixture.Services.GetRequiredService<ILogger<IdempotencyPurgeHostedService>>(),
                fixture.Services.GetRequiredService<IOptions<IdempotencyOptions>>());
        }

        [IgnoreOnCIFact(DisplayName = "In-progress record past InFlightTtl is recovered to retryable")]
        public async Task Stale_in_progress_is_recovered_Async()
        {
            await using var fixture = await EfIdempotencyFixture.CreateAsync(
                new IdempotencyOptions { InFlightTtl = TimeSpan.FromMinutes(15) });
            const string scope = "t:orders";
            const string key = "stuck-1";

            using (var s = fixture.Services.CreateScope())
            {
                var svc = s.ServiceProvider.GetRequiredService<IIdempotencyService>();
                (await svc.TryBeginRequestAsync(scope, key, "h")).Outcome.Should().Be(IdempotencyOutcome.Created);
            }

            // Simulate a crashed node: age LockedAt well past the in-flight TTL.
            using (var s = fixture.Services.CreateScope())
            {
                var ctx = s.ServiceProvider.GetRequiredService<IdempotencyContext>();
                await ctx.IdempotencyRecords.Where(r => r.Scope == scope && r.Key == key)
                    .ExecuteUpdateAsync(u => u.SetProperty(r => r.LockedAt, DateTimeOffset.Now.AddHours(-1)));
            }

            var (recovered, _) = await CreatePurge(fixture).RunOnceAsync(CancellationToken.None);
            recovered.Should().Be(1);

            // Recovered → a retry is allowed to execute again.
            using (var s = fixture.Services.CreateScope())
            {
                var svc = s.ServiceProvider.GetRequiredService<IIdempotencyService>();
                (await svc.TryBeginRequestAsync(scope, key, "h")).Outcome.Should().Be(IdempotencyOutcome.Created);
            }
        }

        [IgnoreOnCIFact(DisplayName = "A not-applied failure leaves the key retryable (FR-008)")]
        public async Task Failed_attempt_is_retryable_Async()
        {
            await using var fixture = await EfIdempotencyFixture.CreateAsync();
            const string scope = "t:orders";
            const string key = "failed-1";

            using (var s = fixture.Services.CreateScope())
            {
                var svc = s.ServiceProvider.GetRequiredService<IIdempotencyService>();
                (await svc.TryBeginRequestAsync(scope, key, "h")).Outcome.Should().Be(IdempotencyOutcome.Created);
                await svc.TryCompleteRequestAsync(scope, key, success: false);
            }

            using (var s = fixture.Services.CreateScope())
            {
                var svc = s.ServiceProvider.GetRequiredService<IIdempotencyService>();
                (await svc.TryBeginRequestAsync(scope, key, "h")).Outcome.Should().Be(IdempotencyOutcome.Created);
            }
        }
    }
}
