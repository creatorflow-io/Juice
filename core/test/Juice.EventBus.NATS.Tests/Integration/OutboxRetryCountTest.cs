using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Tests.Events;
using Juice.EventBus.Publishing;
using Juice.Messaging.Outbox;
using Juice.Messaging.Policies;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    /// <summary>
    /// Outbox RetryCount bookkeeping: retry-failed backoff ends after MaxRetryAttempts, and recover-timeout
    /// does not reclaim a stuck delivery forever. The publisher never reaches NATS, but the host needs SQL Server.
    /// </summary>
    [InitializeMessageContext]
    [Collection(NatsServerCollection.Name)]
    public class OutboxRetryCountTest
    {
        private const int MaxRetryAttempts = 2;
        private readonly ITestOutputHelper _output;

        public OutboxRetryCountTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private OutboxTestHost CreateHost(FailingPublisher publisher)
            => OutboxTestHost.Create(_output, _ => [new PublishRoute("nats", "xunit.retries")],
                producerKey: "nats-inner",
                maxRetryAttempts: MaxRetryAttempts,
                configure: services => services.AddKeyedSingleton<ITransportPublisher>("nats", publisher));

        [IgnoreOnCIFact(DisplayName = "Failed delivery should stop retrying after MaxRetryAttempts")]
        public async Task FailedDeliveryShouldStopAfterMaxRetriesAsync()
        {
            var publisher = new FailingPublisher();
            await using var host = CreateHost(publisher);
            await host.StartAsync();

            await host.PublishAsync(new ContentPublishedIntegrationEvent("always fails"));

            await Waiter.WaitAsync(() => host.GetDeliveriesAsync().Result
                .All(d => d.State == DeliveryState.Failed && d.NextAttemptOn == null), TimeSpan.FromSeconds(15));
            var delivery = (await host.GetDeliveriesAsync()).Should().ContainSingle().Subject;
            delivery.RetryCount.Should().Be(MaxRetryAttempts);
            publisher.Attempts.Should().Be(MaxRetryAttempts + 1);
        }

        [IgnoreOnCIFact(DisplayName = "Stuck delivery should be abandoned after MaxRetryAttempts reclaims")]
        public async Task StuckDeliveryShouldBeAbandonedAfterMaxRetriesAsync()
        {
            var publisher = new FailingPublisher();
            await using var host = CreateHost(publisher);
            await host.PublishAsync(new ContentPublishedIntegrationEvent("stuck"));

            // a worker that crashed mid-publish after using up every retry
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<NatsOutboxContext>();
                var stale = DateTimeOffset.UtcNow.AddMinutes(-10);
                await context.OutboxDeliveries.ExecuteUpdateAsync(d => d
                    .SetProperty(e => e.State, DeliveryState.InProgress)
                    .SetProperty(e => e.RetryCount, MaxRetryAttempts)
                    .SetProperty(e => e.ProcessedOn, stale));
            }
            await host.StartAsync();

            await Waiter.WaitAsync(() => host.GetDeliveriesAsync().Result.All(d => d.State == DeliveryState.Failed),
                TimeSpan.FromSeconds(15));
            var delivery = (await host.GetDeliveriesAsync()).Should().ContainSingle().Subject;
            delivery.NextAttemptOn.Should().BeNull();
            delivery.RetryCount.Should().Be(MaxRetryAttempts + 1);
            delivery.LastError.Should().StartWith("Abandoned");
            publisher.Attempts.Should().Be(0);
        }

        private sealed class FailingPublisher : ITransportPublisher
        {
            private int _attempts;
            public int Attempts => _attempts;
            public string Key => "nats";

            public ValueTask PublishAsync(byte[] payload, PublishContext context, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _attempts);
                throw new InvalidOperationException("Broker unavailable (test)");
            }
        }
    }
}
