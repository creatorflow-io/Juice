using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EventBus.Publishing;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Delivery;
using Juice.Messaging.Outbox.Delivery.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Juice.Integrations.Tests
{
    /// <summary>
    /// How a delivery worker treats a batch: one claim, sends that respect the transaction, one commit for what was sent.
    /// </summary>
    public class DeliveryBatchTest
    {
        private static readonly DeliveryPolicy Policy = DeliveryPolicy.Default with { BatchSize = 10 };

        [Fact(DisplayName = "Messages of one transaction go out in order, while other transactions overtake")]
        public async Task Transaction_order_is_kept_and_transactions_run_concurrentlyAsync()
        {
            var publisher = new RecordingPublisher { SupportsConcurrentPublish = true, Delay = TimeSpan.FromMilliseconds(5) };
            string[] transactions = ["a", "b", "c", "d"];
            var deliveries = transactions.SelectMany(t => Enumerable.Range(0, 5).Select(i => Delivery(t))).ToArray();

            await RunAsync(deliveries, publisher, new FakeRepository(), Policy with { BatchSize = deliveries.Length });

            publisher.Sent.Should().HaveCount(deliveries.Length);
            publisher.MostAtOnce.Should().BeGreaterThan(1, "different transactions may be sent at the same time");
            foreach (var transaction in transactions)
            {
                var inOrder = deliveries.Where(d => d.OutboxEvent.TransactionId == transaction).Select(d => d.EventId).ToArray();
                publisher.Sent.Where(inOrder.Contains).Should().Equal(inOrder, "messages of one transaction keep their order");
            }
        }

        [Fact(DisplayName = "MaxConcurrency 1 sends everything in order")]
        public async Task One_at_a_time_on_requestAsync()
        {
            var publisher = new RecordingPublisher { SupportsConcurrentPublish = true, Delay = TimeSpan.FromMilliseconds(1) };
            var deliveries = Enumerable.Range(0, 10).Select(_ => Delivery(null)).ToArray();

            await RunAsync(deliveries, publisher, new FakeRepository(), Policy with { MaxConcurrency = 1 });

            publisher.MostAtOnce.Should().Be(1);
            publisher.Sent.Should().Equal(deliveries.Select(d => d.EventId));
        }

        [Fact(DisplayName = "A publisher that doesn't support it is never called concurrently")]
        public async Task Publisher_without_support_stays_one_at_a_timeAsync()
        {
            var publisher = new RecordingPublisher { Delay = TimeSpan.FromMilliseconds(1) };
            var deliveries = Enumerable.Range(0, 10).Select(_ => Delivery(null)).ToArray();

            await RunAsync(deliveries, publisher, new FakeRepository(), Policy);

            publisher.MostAtOnce.Should().Be(1);
            publisher.Sent.Should().Equal(deliveries.Select(d => d.EventId));
        }

        [Fact(DisplayName = "A batch takes one claim and one commit for what was sent")]
        public async Task One_commit_each_wayAsync()
        {
            var repository = new FakeRepository();
            var deliveries = Enumerable.Range(0, 10).Select(_ => Delivery(null)).ToArray();

            await RunAsync(deliveries, new RecordingPublisher { SupportsConcurrentPublish = true }, repository, Policy);

            repository.Claims.Should().Be(1);
            repository.PublishedCommits.Should().Be(1);
            repository.Published.Should().BeEquivalentTo(deliveries.Select(d => d.DeliveryId));
        }

        [Fact(DisplayName = "Only what this worker claimed is sent")]
        public async Task Deliveries_claimed_elsewhere_are_not_sentAsync()
        {
            var deliveries = Enumerable.Range(0, 10).Select(_ => Delivery(null)).ToArray();
            var lost = deliveries.Take(4).Select(d => d.DeliveryId).ToHashSet();
            var repository = new FakeRepository { ClaimedElsewhere = lost };
            var publisher = new RecordingPublisher { SupportsConcurrentPublish = true };

            await RunAsync(deliveries, publisher, repository, Policy);

            publisher.Sent.Should().BeEquivalentTo(deliveries.Skip(4).Select(d => d.EventId));
            repository.Published.Should().NotContain(lost);
        }

        [Fact(DisplayName = "A failed send is retried later and doesn't stop the others")]
        public async Task A_failure_affects_only_its_messageAsync()
        {
            var deliveries = Enumerable.Range(0, 10).Select(_ => Delivery(null)).ToArray();
            var failing = deliveries[3];
            var repository = new FakeRepository();
            var publisher = new RecordingPublisher { SupportsConcurrentPublish = true, FailFor = failing.EventId };

            var moreWaiting = await RunAsync(deliveries, publisher, repository, Policy);

            repository.Failed.Keys.Should().Equal(failing.DeliveryId);
            repository.Failed[failing.DeliveryId].Should().NotBeNull("it gets another attempt");
            repository.Published.Should().BeEquivalentTo(deliveries.Except([failing]).Select(d => d.DeliveryId));
            moreWaiting.Should().BeFalse("a full batch with a failure waits, so a broker outage doesn't spin");
        }

        [Fact(DisplayName = "A deferred delivery waits, without counting as a failure")]
        public async Task A_deferred_delivery_is_not_a_failureAsync()
        {
            var deliveries = Enumerable.Range(0, 10).Select(_ => Delivery(null)).ToArray();
            var waiting = deliveries[3];
            var repository = new FakeRepository();
            var publisher = new RecordingPublisher { DeferFor = waiting.EventId };

            var before = DateTimeOffset.UtcNow;
            var moreWaiting = await RunAsync(deliveries, publisher, repository, Policy);

            repository.Deferred.Keys.Should().Equal(waiting.DeliveryId);
            repository.Deferred[waiting.DeliveryId].Should().BeOnOrAfter(before + Policy.InitialRetryDelay);
            repository.Failed.Should().BeEmpty();
            repository.Published.Should().BeEquivalentTo(deliveries.Except([waiting]).Select(d => d.DeliveryId));
            moreWaiting.Should().BeTrue("retrieval skips it until it's due, so the rest of the queue doesn't have to wait");
        }

        [Fact(DisplayName = "A reclaimed delivery can be deferred too")]
        public async Task A_reclaimed_delivery_can_be_deferredAsync()
        {
            var delivery = Delivery(null);
            delivery.UpdateState(DeliveryState.InProgress);
            var repository = new FakeRepository();

            await RunAsync([delivery], new RecordingPublisher { DeferFor = delivery.EventId }, repository, Policy);

            repository.Deferred.Keys.Should().Equal(delivery.DeliveryId);
            repository.Failed.Should().BeEmpty();
            repository.Published.Should().BeEmpty();
        }

        [Fact(DisplayName = "A repository that can't defer marks the delivery failed, to be retried")]
        public async Task Repository_without_deferAsync()
        {
            var repository = new SingleCallRepository();
            var delivery = Delivery(null);

            await RunAsync([delivery], new RecordingPublisher { DeferFor = delivery.EventId }, repository, Policy);

            repository.Published.Should().BeEmpty();
            repository.Failed.Keys.Should().Equal(delivery.DeliveryId);
            repository.Failed[delivery.DeliveryId].Should().NotBeNull("it gets another attempt");
        }

        [Fact(DisplayName = "Stopping keeps a record of what the broker already has")]
        public async Task Stopping_still_marks_what_was_sentAsync()
        {
            using var stopping = new CancellationTokenSource();
            var deliveries = Enumerable.Range(0, 3).Select(_ => Delivery(null)).ToArray();
            var repository = new FakeRepository();
            var publisher = new RecordingPublisher { CancelAfterFirst = stopping };

            var run = () => RunAsync(deliveries, publisher, repository, Policy with { MaxConcurrency = 1 }, stopping.Token);

            await run.Should().ThrowAsync<OperationCanceledException>();
            repository.Published.Should().Equal(deliveries[0].DeliveryId);
        }

        [Theory(DisplayName = "The next batch doesn't wait while deliveries are waiting")]
        [InlineData(10, true)]
        [InlineData(9, false)]
        [InlineData(0, false)]
        public async Task A_full_batch_asks_for_the_nextAsync(int retrieved, bool expected)
        {
            var deliveries = Enumerable.Range(0, retrieved).Select(_ => Delivery(null)).ToArray();

            var moreWaiting = await RunAsync(deliveries, new RecordingPublisher(), new FakeRepository(), Policy);

            moreWaiting.Should().Be(expected);
        }

        [Fact(DisplayName = "Losing a whole batch to another worker still asks for the next")]
        public async Task Losing_a_batch_doesnt_waitAsync()
        {
            var deliveries = Enumerable.Range(0, 10).Select(_ => Delivery(null)).ToArray();
            var repository = new FakeRepository { ClaimedElsewhere = deliveries.Select(d => d.DeliveryId).ToHashSet() };

            var moreWaiting = await RunAsync(deliveries, new RecordingPublisher(), repository, Policy);

            moreWaiting.Should().BeTrue("the other worker changed those rows, and this one would otherwise sit out the interval");
        }

        [Fact(DisplayName = "A repository with only the single calls still works")]
        public async Task Repository_without_batch_callsAsync()
        {
            var repository = new SingleCallRepository();
            var deliveries = Enumerable.Range(0, 4).Select(_ => Delivery(null)).ToArray();
            repository.Lost = deliveries[1].DeliveryId;
            var publisher = new RecordingPublisher { SupportsConcurrentPublish = true };

            await RunAsync(deliveries, publisher, repository, Policy);

            publisher.Sent.Should().HaveCount(3);
            repository.Published.Should().BeEquivalentTo(deliveries.Where(d => d.DeliveryId != repository.Lost).Select(d => d.DeliveryId));
        }

        [Fact(DisplayName = "A reclaimed delivery still counts its attempts and gives up")]
        public async Task Reclaims_keep_their_limitAsync()
        {
            var repository = new FakeRepository();
            var within = Delivery(null);
            within.UpdateState(DeliveryState.InProgress);
            var spent = Delivery(null);
            for (var attempt = 0; attempt < Policy.MaxRetryAttempts; attempt++)
            {
                spent.UpdateState(DeliveryState.Failed);
                spent.UpdateState(DeliveryState.InProgress);
            }
            var publisher = new RecordingPublisher();

            var moreWaiting = await RunAsync([within, spent], publisher, repository, Policy);

            publisher.Sent.Should().Equal(within.EventId);
            repository.Reclaimed.Should().BeEquivalentTo([within.DeliveryId, spent.DeliveryId]);
            repository.Published.Should().Equal(within.DeliveryId);
            repository.Failed.Keys.Should().Equal(spent.DeliveryId);
            repository.Failed[spent.DeliveryId].Should().BeNull("it gets no further attempt");
            moreWaiting.Should().BeFalse();
        }

        private static OutboxDelivery Delivery(string? transaction)
        {
            var eventId = Guid.NewGuid();
            return new OutboxDelivery
            {
                DeliveryId = Guid.NewGuid(),
                EventId = eventId,
                PublisherKey = "test",
                Destination = "x",
                OutboxEvent = new OutboxEvent
                {
                    EventId = eventId,
                    EventTypeName = "Thing",
                    PayloadBytes = [],
                    TransactionId = transaction,
                }
            };
        }

        private static async Task<bool> RunAsync(OutboxDelivery[] deliveries, RecordingPublisher publisher, IOutboxRepository<WorkerContext> repository,
            DeliveryPolicy policy, CancellationToken cancellationToken = default)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped(_ => repository);
            services.AddKeyedScoped<IDeliveryIntent<WorkerContext>>("send-pending", (_, _) => new FixedIntent(deliveries));
            services.AddKeyedScoped<ITransportPublisher>("test", (_, _) => publisher);
            _ = new DeliveryProcessorBuilder("test", services);
            await using var provider = services.BuildServiceProvider();

            var worker = new DeliveryWorker<WorkerContext>(provider, "test", "send-pending", new FixedPolicy(policy),
                NullLogger<DeliveryWorker<WorkerContext>>.Instance);
            await worker.InitializeAsync(cancellationToken);
            return await worker.ExecuteAsync(cancellationToken);
        }

        public sealed class WorkerContext { }

        private sealed class FixedPolicy(DeliveryPolicy policy) : IDeliveryPolicyResolver
        {
            public ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken)
                => ValueTask.FromResult(policy);
        }

        private sealed class FixedIntent(OutboxDelivery[] deliveries) : IDeliveryIntent<WorkerContext>
        {
            public string Name => "send-pending";
            public Task<IEnumerable<OutboxDelivery>> RetrieveDeliveriesAsync(string publisherKey, DeliveryPolicy policy, CancellationToken cancellationToken)
                => Task.FromResult<IEnumerable<OutboxDelivery>>(deliveries);
        }

        private sealed class RecordingPublisher : ITransportPublisher
        {
            private int _atOnce;
            public string Key => "test";
            public bool SupportsConcurrentPublish { get; init; }
            public TimeSpan Delay { get; init; }
            public Guid? FailFor { get; init; }
            public Guid? DeferFor { get; init; }
            public CancellationTokenSource? CancelAfterFirst { get; init; }
            private readonly ConcurrentQueue<Guid> _sent = new();
            public Guid[] Sent => _sent.ToArray();
            public int MostAtOnce;

            public async ValueTask PublishAsync(byte[] payload, PublishContext context, CancellationToken cancellationToken = default)
            {
                var now = Interlocked.Increment(ref _atOnce);
                int most;
                while (now > (most = MostAtOnce) && Interlocked.CompareExchange(ref MostAtOnce, now, most) != most) { }
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(Delay, cancellationToken);
                    var id = Guid.Parse(context.MessageId);
                    if (id == FailFor)
                    {
                        throw new InvalidOperationException("The broker refused it");
                    }
                    if (id == DeferFor)
                    {
                        throw new DeliveryDeferredException("Not yet");
                    }
                    _sent.Enqueue(id);
                    CancelAfterFirst?.Cancel();
                }
                finally
                {
                    Interlocked.Decrement(ref _atOnce);
                }
            }
        }

        private class FakeRepository : IOutboxRepository<WorkerContext>
        {
            public HashSet<Guid> ClaimedElsewhere { get; init; } = [];
            public int Claims;
            public int PublishedCommits;
            public List<Guid> Published { get; } = [];
            public List<Guid> Reclaimed { get; } = [];
            public Dictionary<Guid, DateTimeOffset?> Failed { get; } = [];
            public Dictionary<Guid, DateTimeOffset> Deferred { get; } = [];

            public ValueTask DeferAsync(Guid deliveryId, string reason, DateTimeOffset nextAttempt, CancellationToken cancellationToken = default)
            {
                Deferred[deliveryId] = nextAttempt;
                return ValueTask.CompletedTask;
            }

            public ValueTask<IReadOnlyCollection<Guid>> MarkAsInProgressAsync(IReadOnlyCollection<Guid> deliveryIds, CancellationToken cancellationToken = default)
            {
                Claims++;
                return ValueTask.FromResult<IReadOnlyCollection<Guid>>(deliveryIds.Except(ClaimedElsewhere).ToArray());
            }

            public ValueTask MarkAsPublishedAsync(IReadOnlyCollection<Guid> deliveryIds, CancellationToken cancellationToken = default)
            {
                PublishedCommits++;
                Published.AddRange(deliveryIds);
                return ValueTask.CompletedTask;
            }

            public ValueTask MarkAsPublishedAsync(Guid deliveryId, CancellationToken cancellationToken = default)
            {
                Published.Add(deliveryId);
                return ValueTask.CompletedTask;
            }

            public ValueTask<int> ReclaimInProgressAsync(Guid deliveryId, DateTimeOffset? staleProcessedOn, CancellationToken cancellationToken = default)
            {
                Reclaimed.Add(deliveryId);
                return ValueTask.FromResult(1);
            }

            public ValueTask MarkAsFailedAsync(Guid deliveryId, string error, DateTimeOffset? nextAttempt, CancellationToken cancellationToken = default)
            {
                Failed[deliveryId] = nextAttempt;
                return ValueTask.CompletedTask;
            }

            public ValueTask<int> MarkAsInProgressAsync(Guid deliveryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask MarkAsSkippedAsync(Guid deliveryId, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask SaveEventsAsync(OutboxEvent[] @event, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void Dispose() { }
        }

        /// <summary>Written before the batch calls existed: only the interface's own defaults make it work.</summary>
        private sealed class SingleCallRepository : IOutboxRepository<WorkerContext>
        {
            public Guid Lost { get; set; }
            public List<Guid> Published { get; } = [];
            public Dictionary<Guid, DateTimeOffset?> Failed { get; } = [];

            public ValueTask<int> MarkAsInProgressAsync(Guid deliveryId, CancellationToken cancellationToken = default)
                => ValueTask.FromResult(deliveryId == Lost ? 0 : 1);

            public ValueTask MarkAsPublishedAsync(Guid deliveryId, CancellationToken cancellationToken = default)
            {
                Published.Add(deliveryId);
                return ValueTask.CompletedTask;
            }

            public ValueTask MarkAsFailedAsync(Guid deliveryId, string error, DateTimeOffset? nextAttempt, CancellationToken cancellationToken = default)
            {
                Failed[deliveryId] = nextAttempt;
                return ValueTask.CompletedTask;
            }

            public ValueTask MarkAsSkippedAsync(Guid deliveryId, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask SaveEventsAsync(OutboxEvent[] @event, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}
