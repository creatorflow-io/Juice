using FluentAssertions;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Idempotency.EF;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Idempotency.Tests
{
    // US1: the EF unique constraint on (Scope, Key) serializes concurrent first-time arrivals.
    public class ConcurrentCreateTests
    {
        [IgnoreOnCIFact(DisplayName = "Concurrent TryBegin on same (Scope,Key) → exactly one Created")]
        public async Task Concurrent_begin_yields_single_winner_Async()
        {
            await using var fixture = await EfIdempotencyFixture.CreateAsync();
            const string scope = "t:orders";
            const string key = "concurrent-1";

            async Task<IdempotencyOutcome> BeginAsync()
            {
                using var scopeSp = fixture.Services.CreateScope();
                var svc = scopeSp.ServiceProvider.GetRequiredService<IIdempotencyService>();
                var res = await svc.TryBeginRequestAsync(scope, key, "hashA");
                return res.Outcome;
            }

            var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => BeginAsync()));

            outcomes.Count(o => o == IdempotencyOutcome.Created).Should().Be(1);
            outcomes.Count(o => o == IdempotencyOutcome.InProgress).Should().Be(7);
        }
    }
}
