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
    [InitializeMessageContext]
    [Collection(NatsServerCollection.Name)]
    public class JetStreamOutboxEndToEndTest
    {
        private readonly ITestOutputHelper _output;

        public JetStreamOutboxEndToEndTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [IgnoreOnCIFact(DisplayName = "Outbox event should be published to JetStream")]
        public async Task OutboxEventShouldBePublishedToJetStreamAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.outbox");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            await using var host = OutboxTestHost.Create(_output, _ => [new PublishRoute("nats", destination)]);
            try
            {
                await host.StartAsync();
                var evt = new ContentPublishedIntegrationEvent("hello outbox");
                await host.PublishAsync(evt);

                await Waiter.WaitAsync(() => host.GetDeliveriesAsync().Result.All(d => d.State == DeliveryState.Published),
                    TimeSpan.FromSeconds(5));

                var deliveries = await host.GetDeliveriesAsync();
                deliveries.Should().ContainSingle().Which.State.Should().Be(DeliveryState.Published);

                var stored = await NatsTestServer.ReadAllAsync(stream);
                var message = stored.Should().ContainSingle().Subject;
                message.Subject.Should().Be($"{destination}.{nameof(ContentPublishedIntegrationEvent)}");
                message.Headers["Nats-Msg-Id"].Should().Be($"{evt.MessageId}:{message.Subject}");
                message.Headers["x-message-id"].Should().Be(evt.MessageId.ToString());
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Routing to two publishers should deliver independently")]
        public async Task PolicyRoutingToTwoPublishersShouldDeliverIndependentlyAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.outbox");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            await using var host = OutboxTestHost.Create(_output,
                _ => [new PublishRoute("nats", destination), new PublishRoute("failing", "anywhere")],
                configure: services => services.AddKeyedSingleton<ITransportPublisher>("failing", new FailingPublisher()),
                publishers: ["nats", "failing"]);
            try
            {
                await host.StartAsync();
                await host.PublishAsync(new ContentPublishedIntegrationEvent("two routes"));

                await Waiter.WaitAsync(() => host.GetDeliveriesAsync().Result
                    .Any(d => d.PublisherKey == "failing" && d.State == DeliveryState.Failed)
                    && host.GetDeliveriesAsync().Result
                    .Any(d => d.PublisherKey == "nats" && d.State == DeliveryState.Published),
                    TimeSpan.FromSeconds(5));

                var deliveries = await host.GetDeliveriesAsync();
                deliveries.Single(d => d.PublisherKey == "nats").State.Should().Be(DeliveryState.Published);
                deliveries.Single(d => d.PublisherKey == "failing").State.Should().Be(DeliveryState.Failed);
                (await NatsTestServer.ReadAllAsync(stream)).Should().ContainSingle();
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Delivery should fail, retry and publish once the stream is available")]
        public async Task DeliveryShouldRecoverWhenStreamBecomesAvailableAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.outbox");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await using var host = OutboxTestHost.Create(_output, _ => [new PublishRoute("nats", destination)]);
            try
            {
                await host.StartAsync();
                await host.PublishAsync(new ContentPublishedIntegrationEvent("no stream yet"));

                await Waiter.WaitAsync(() => host.GetDeliveriesAsync().Result.Any(d => d.State == DeliveryState.Failed), TimeSpan.FromSeconds(10));
                (await host.GetDeliveriesAsync()).Single().State.Should().Be(DeliveryState.Failed);

                await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");

                await Waiter.WaitAsync(() => host.GetDeliveriesAsync().Result.All(d => d.State == DeliveryState.Published), TimeSpan.FromSeconds(10));
                (await host.GetDeliveriesAsync()).Single().State.Should().Be(DeliveryState.Published);
                (await NatsTestServer.ReadAllAsync(stream)).Should().ContainSingle();
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Round trip p95 through outbox, JetStream and handler should be under 1 s")]
        public async Task RoundTripP95ShouldBeUnder1sAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.roundtrip");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            var probe = new HandlerProbe();
            await using var host = OutboxTestHost.Create(_output, _ => [new PublishRoute("nats", destination)],
                jetStream: js => js.AddConsumer("nats.roundtrip", stream, "roundtrip", ConsumerTestHost.ConsumerConnection, c => c
                    .Destination(destination)
                    .Provisioning(Infrastructure.JetStreamProvisioning.CreateIfMissing)
                    .MaxConcurrency(20)
                    .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>()),
                configure: services => services.AddSingleton(probe));
            try
            {
                await host.StartAsync();
                var published = new System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTimeOffset>();
                for (var i = 0; i < 200; i++)
                {
                    var evt = new ContentPublishedIntegrationEvent($"round trip {i}");
                    published[evt.MessageId] = DateTimeOffset.UtcNow;
                    await host.PublishAsync(evt);
                }

                await Waiter.WaitAsync(() => probe.Completed.Count >= 200, TimeSpan.FromSeconds(30));
                probe.Completed.Count.Should().Be(200);

                var latencies = probe.Calls
                    .GroupBy(c => c.MessageId)
                    .Select(g => (g.Min(c => c.At) - published[g.Key]).TotalMilliseconds)
                    .OrderBy(ms => ms)
                    .ToArray();
                var p95 = latencies[(int)Math.Ceiling(latencies.Length * 0.95) - 1];
                _output.WriteLine($"Round trip p50 {latencies[latencies.Length / 2]:n0} ms, p95 {p95:n0} ms, max {latencies[^1]:n0} ms");
                p95.Should().BeLessThan(1000);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "A 10k backlog should drain within 30 s")]
        public async Task BacklogOf10kShouldDrainWithin30sAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.backlog10k");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            try
            {
                await using (var seeder = ConsumerTestHost.Create(_output, destination, stream, "backlog10k"))
                {
                    for (var batch = 0; batch < 100; batch++)
                    {
                        await Task.WhenAll(Enumerable.Range(0, 100)
                            .Select(i => seeder.PublishAsync(destination, $"backlog {batch}-{i}")));
                    }
                }

                await using var host = ConsumerTestHost.Create(_output, destination, stream, "backlog10k",
                    consumer: c => c.MaxConcurrency(100));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await host.StartAsync();

                await Waiter.WaitAsync(() => host.Probe.Completed.Count >= 10_000, TimeSpan.FromSeconds(30));
                _output.WriteLine($"10k backlog drained in {sw.Elapsed}");

                host.Probe.Completed.Count.Should().Be(10_000);
                sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        /// <summary>
        /// SC-002: no event is lost or handled twice when acks are lost and the delivery host restarts mid-batch.
        /// The server restart step runs only when <c>NATS_DOCKER_CONTAINER</c> names the nats container.
        /// A delivery the first host leaves <c>InProgress</c> is re-claimed by the second host's recover-timeout intent.
        /// </summary>
        [IgnoreOnCIFact(DisplayName = "Outbox delivery should survive faults without loss or duplicates")]
        public async Task OutboxDeliveryShouldSurviveFaultsAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.faults");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            var connectionString = OutboxTestHost.NewConnectionString();
            var lostAcks = 0;

            OutboxTestHost CreateDeliveryHost() => OutboxTestHost.Create(_output, _ => [new PublishRoute("nats", destination)],
                producerKey: "nats-inner",
                connectionString: connectionString,
                configure: services => services.AddKeyedSingleton<ITransportPublisher>("nats", (sp, _) =>
                    new LostAckPublisher(sp.GetRequiredKeyedService<ITransportPublisher>("nats-inner"), () => Interlocked.Increment(ref lostAcks))));

            await using var consumer = ConsumerTestHost.Create(_output, destination, stream, "faults",
                consumer: c => c.MaxConcurrency(20));
            await consumer.StartAsync();

            var first = CreateDeliveryHost();
            try
            {
                await first.StartAsync();
                var events = Enumerable.Range(0, 500).Select(i => new ContentPublishedIntegrationEvent($"fault {i}")).ToArray();
                foreach (var evt in events)
                {
                    await first.PublishAsync(evt);
                }

                // 1. stop the delivery host mid-batch
                await Waiter.WaitAsync(() => first.GetDeliveriesAsync().Result.Count(d => d.State == DeliveryState.Published) >= 150, TimeSpan.FromSeconds(30));
                await first.StopAsync();
                await first.Services.DisposeAsync();

                // 2. optionally restart the server
                if (!string.IsNullOrEmpty(NatsTestServer.DockerContainer))
                {
                    using var restart = System.Diagnostics.Process.Start("docker", $"restart {NatsTestServer.DockerContainer}");
                    await restart!.WaitForExitAsync();
                }
                else
                {
                    _output.WriteLine("Nats:DockerContainer not set: server restart step skipped.");
                }

                // 3. a new delivery host on the same outbox finishes the batch (including recover-timeout)
                await using var second = CreateDeliveryHost();
                await second.StartAsync();
                await Waiter.WaitAsync(() => second.GetDeliveriesAsync().Result.All(d => d.State == DeliveryState.Published), TimeSpan.FromSeconds(60));
                await Waiter.WaitAsync(() => consumer.Probe.Completed.Count >= 500, TimeSpan.FromSeconds(30));

                var deliveries = await second.GetDeliveriesAsync();
                _output.WriteLine($"Lost acks simulated: {lostAcks}");
                lostAcks.Should().BeGreaterThan(0);
                deliveries.Should().HaveCount(500).And.OnlyContain(d => d.State == DeliveryState.Published);

                var stored = await NatsTestServer.ReadAllAsync(stream);
                stored.Should().HaveCount(500);
                stored.DistinctHeader("x-message-id").Should().HaveCount(500);

                consumer.Probe.Completed.Count.Should().Be(500);
                consumer.Probe.Completed.Values.Should().OnlyContain(n => n == 1);
                consumer.Probe.Completed.Keys.Should().BeEquivalentTo(events.Select(e => e.MessageId));
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
                var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<NatsOutboxContext>()
                    .UseSqlServer(connectionString).Options;
                await using var context = new NatsOutboxContext(options);
                await context.Database.EnsureDeletedAsync();
            }
        }

        /// <summary>
        /// Publishes, then throws for every 50th message: the stream has it but the outbox records a failure and retries.
        /// </summary>
        private sealed class LostAckPublisher : ITransportPublisher
        {
            private readonly ITransportPublisher _inner;
            private readonly Func<int> _onLostAck;
            private int _count;

            public LostAckPublisher(ITransportPublisher inner, Func<int> onLostAck)
            {
                _inner = inner;
                _onLostAck = onLostAck;
            }

            public string Key => "nats";

            public async ValueTask PublishAsync(byte[] payload, PublishContext context, CancellationToken cancellationToken = default)
            {
                await _inner.PublishAsync(payload, context, cancellationToken);
                if (Interlocked.Increment(ref _count) % 50 == 0)
                {
                    _onLostAck();
                    throw new TimeoutException("Ack lost (test)");
                }
            }
        }

        private sealed class FailingPublisher : ITransportPublisher
        {
            public string Key => "failing";

            public ValueTask PublishAsync(byte[] payload, PublishContext context, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Broker unavailable (test)");
        }
    }
}
