using FluentAssertions;
using Juice.Messaging.Idempotency;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Idempotency.Tests
{
    /// <summary>
    /// Verifies <see cref="IIdempotencyService.TryBeginRequestAsync"/> fingerprint conflict detection
    /// (FR-005) across the in-process stores. Redis/EF share the same shape but need live infra, so they
    /// are exercised by the infra-dependent suite; these run everywhere.
    /// </summary>
    public class IdempotencyConflictTests
    {
        private const string Scope = "ConflictScope";

        private static IIdempotencyService BuildManager(string provider)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = services.AddMessaging();
            switch (provider)
            {
                case "DistributedCache":
                    builder.AddIdempotencyDistributedCache();
                    services.AddDistributedMemoryCache();
                    break;
                default:
                    builder.AddIdempotencyInMemory();
                    break;
            }
            return services.BuildServiceProvider().GetRequiredService<IIdempotencyService>();
        }

        [Theory]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Same_key_with_different_hash_conflictsAsync(string provider)
        {
            var manager = BuildManager(provider);
            var key = Guid.NewGuid().ToString();

            var first = await manager.TryBeginRequestAsync(Scope, key, "hash-A");
            first.Outcome.Should().Be(IdempotencyOutcome.Created);

            var second = await manager.TryBeginRequestAsync(Scope, key, "hash-B");
            second.Outcome.Should().Be(IdempotencyOutcome.Conflict);
        }

        [Theory]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Same_key_with_same_hash_does_not_conflictAsync(string provider)
        {
            var manager = BuildManager(provider);
            var key = Guid.NewGuid().ToString();

            (await manager.TryBeginRequestAsync(Scope, key, "hash-A")).Outcome
                .Should().Be(IdempotencyOutcome.Created);

            // Same fingerprint while still in flight → in-progress, never a false conflict.
            (await manager.TryBeginRequestAsync(Scope, key, "hash-A")).Outcome
                .Should().Be(IdempotencyOutcome.InProgress);
        }

        [Theory]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task Conflict_is_detected_after_completionAsync(string provider)
        {
            var manager = BuildManager(provider);
            var key = Guid.NewGuid().ToString();

            (await manager.TryBeginRequestAsync(Scope, key, "hash-A")).Outcome
                .Should().Be(IdempotencyOutcome.Created);
            await manager.TryCompleteRequestAsync(Scope, key, success: true, result: "done");

            // Same fingerprint → completed replay.
            (await manager.TryBeginRequestAsync(Scope, key, "hash-A")).Outcome
                .Should().Be(IdempotencyOutcome.Completed);

            // Different fingerprint after completion → still a conflict (hash retained through retention).
            (await manager.TryBeginRequestAsync(Scope, key, "hash-B")).Outcome
                .Should().Be(IdempotencyOutcome.Conflict);
        }

        [Theory]
        [InlineData("InMemory")]
        [InlineData("DistributedCache")]
        public async Task No_stored_hash_never_conflictsAsync(string provider)
        {
            var manager = BuildManager(provider);
            var key = Guid.NewGuid().ToString();

            // First call supplies no fingerprint (e.g. a store/caller that does not compute one).
            (await manager.TryBeginRequestAsync(Scope, key, requestHash: null)).Outcome
                .Should().Be(IdempotencyOutcome.Created);

            // A later call with a fingerprint cannot conflict against an absent stored hash.
            (await manager.TryBeginRequestAsync(Scope, key, "hash-B")).Outcome
                .Should().Be(IdempotencyOutcome.InProgress);
        }
    }
}
