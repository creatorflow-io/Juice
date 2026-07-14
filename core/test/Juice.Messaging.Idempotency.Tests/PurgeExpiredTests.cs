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
    // US2 / FR-009: records past ExpiresAt are deleted by the purge service so the store stays bounded.
    public class PurgeExpiredTests
    {
        [IgnoreOnCIFact(DisplayName = "Records past ExpiresAt are purged")]
        public async Task Expired_records_are_deleted_Async()
        {
            await using var fixture = await EfIdempotencyFixture.CreateAsync();
            const string scope = "t:orders";
            const string key = "expired-1";

            using (var s = fixture.Services.CreateScope())
            {
                var svc = s.ServiceProvider.GetRequiredService<IIdempotencyService>();
                await svc.TryBeginRequestAsync(scope, key, "h");
                await svc.TryCompleteRequestAsync(scope, key, success: true, result: new { ok = true });
            }

            // Force expiry into the past.
            using (var s = fixture.Services.CreateScope())
            {
                var ctx = s.ServiceProvider.GetRequiredService<IdempotencyContext>();
                await ctx.IdempotencyRecords.Where(r => r.Scope == scope && r.Key == key)
                    .ExecuteUpdateAsync(u => u.SetProperty(r => r.ExpiresAt, DateTimeOffset.Now.AddHours(-1)));
            }

            var purge = new IdempotencyPurgeHostedService(
                fixture.Services.GetRequiredService<IServiceScopeFactory>(),
                fixture.Services.GetRequiredService<ILogger<IdempotencyPurgeHostedService>>(),
                fixture.Services.GetRequiredService<IOptions<IdempotencyOptions>>());

            var (_, purged) = await purge.RunOnceAsync(CancellationToken.None);
            purged.Should().BeGreaterThanOrEqualTo(1);

            using (var s = fixture.Services.CreateScope())
            {
                var ctx = s.ServiceProvider.GetRequiredService<IdempotencyContext>();
                (await ctx.IdempotencyRecords.AnyAsync(r => r.Scope == scope && r.Key == key)).Should().BeFalse();
            }
        }
    }
}
