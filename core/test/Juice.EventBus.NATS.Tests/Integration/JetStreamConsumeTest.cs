using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using FluentAssertions;
using Juice.EF.Tests.Events;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    [InitializeMessageContext]
    [Collection(NatsServerCollection.Name)]
    public class JetStreamConsumeTest
    {
        private readonly ITestOutputHelper _output;

        public JetStreamConsumeTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private static async Task<(string Destination, string Stream)> NewStreamAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.consume");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            return (destination, stream);
        }

        private static async Task AssertNothingPendingAsync(string stream, string durable)
        {
            await Waiter.WaitAsync(() =>
            {
                var info = NatsTestServer.GetConsumerAsync(stream, durable).Result;
                return info != null && info.NumAckPending == 0 && info.NumPending == 0;
            }, TimeSpan.FromSeconds(5));
            var consumer = await NatsTestServer.GetConsumerAsync(stream, durable);
            consumer!.NumAckPending.Should().Be(0);
            consumer.NumPending.Should().Be(0UL);
        }

        [IgnoreOnCIFact(DisplayName = "Handler should receive the event once and ack it")]
        public async Task HandlerShouldReceiveEventOnceAndAckAsync()
        {
            var (destination, stream) = await NewStreamAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "durable-1");
                await host.StartAsync();

                var evt = await host.PublishAsync(destination, "hello consumer");

                await Waiter.WaitAsync(() => host.Probe.CompletedCount >= 1, TimeSpan.FromSeconds(5));
                host.Probe.Completed.Should().ContainKey(evt.MessageId).WhoseValue.Should().Be(1);
                await AssertNothingPendingAsync(stream, "durable-1");
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Handler should run in the tenant context of the message")]
        public async Task HandlerShouldRunInTenantContextAsync()
        {
            var (destination, stream) = await NewStreamAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "durable-1",
                    configure: services =>
                    {
                        services.AddMultiTenant()
                            .WithInMemoryStore(store =>
                            {
                                store.Tenants.Add(new Juice.Extensions.MultiTenant.TenantInfo("tenant-a-id", "tenant-a", "A", tier: "enterprise"));
                                store.Tenants.Add(new Juice.Extensions.MultiTenant.TenantInfo("tenant-b-id", "tenant-b", "B", tier: "free"));
                            });
                        services.AddHttpContextAccessor();
                    });
                await host.StartAsync();

                var a = await host.PublishAsync(destination, "for tenant a", tenantId: "tenant-a-id");
                var b = await host.PublishAsync(destination, "for tenant b", tenantId: "tenant-b-id");

                await Waiter.WaitAsync(() => host.Probe.CompletedCount >= 2, TimeSpan.FromSeconds(5));
                host.Probe.Calls.Single(c => c.MessageId == a.MessageId).Tenant.Should().Be("tenant-a");
                host.Probe.Calls.Single(c => c.MessageId == b.MessageId).Tenant.Should().Be("tenant-b");
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "MessageContext should carry the correlation id")]
        public async Task MessageContextShouldPropagateCorrelationAsync()
        {
            var (destination, stream) = await NewStreamAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "durable-1");
                await host.StartAsync();

                var evt = await host.PublishAsync(destination, "with context", correlationId: "corr-xyz");

                await Waiter.WaitAsync(() => host.Probe.CompletedCount >= 1, TimeSpan.FromSeconds(5));
                var call = host.Probe.Calls.Single();
                call.Context!.CorrelationId.Should().Be("corr-xyz");
                call.Context.CausationId.Should().Be(evt.MessageId.ToString());
                call.Context.Source.Should().Be("nats.xunit");
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Same message with different broker ids should be handled once")]
        public async Task DuplicatePublishWithIdempotencyShouldHandleOnceAsync()
        {
            var (destination, stream) = await NewStreamAsync();
            try
            {
                // one at a time: the cache-based idempotency store used here is not atomic for truly concurrent
                // duplicates (use Redis/EF in production); this test covers a redelivered copy
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "durable-1",
                    consumer: c => c.MaxConcurrency(1));
                await host.StartAsync();

                var evt = new ContentPublishedIntegrationEvent("twice");
                var subject = $"{destination}.{nameof(ContentPublishedIntegrationEvent)}";
                var headers = ConsumerTestHost.Headers(evt);
                await NatsTestServer.PublishRawAsync(subject, host.Serialize(evt), headers, msgId: "broker-id-1");
                await NatsTestServer.PublishRawAsync(subject, host.Serialize(evt), headers, msgId: "broker-id-2");

                await Waiter.WaitAsync(() => host.Probe.CompletedCount >= 1, TimeSpan.FromSeconds(5));
                await AssertNothingPendingAsync(stream, "durable-1");
                host.Probe.Attempts[evt.MessageId].Should().Be(1);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Wildcard route should only receive matching subjects")]
        public async Task WildcardRouteShouldMatchOnlyMatchingSubjectsAsync()
        {
            var (destination, stream) = await NewStreamAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "durable-1",
                    consumer: c => c.Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>("kernel.*"));
                await host.StartAsync();

                var audit = await host.PublishAsync(destination, "audit", routingKey: "kernel.audit");
                var other = await host.PublishAsync(destination, "other", routingKey: "other.audit");
                var deep = await host.PublishAsync(destination, "deep", routingKey: "kernel.audit.deep");

                await Waiter.WaitAsync(() => host.Probe.Completed.ContainsKey(audit.MessageId), TimeSpan.FromSeconds(5));
                await Task.Delay(500);

                host.Probe.Completed.Should().ContainKey(audit.MessageId);
                host.Probe.Completed.Should().NotContainKey(other.MessageId);
                host.Probe.Completed.Should().NotContainKey(deep.MessageId);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Two instances on the same durable should share the work")]
        public async Task TwoInstancesSameDurableShouldShareWorkAsync()
        {
            var (destination, stream) = await NewStreamAsync();
            try
            {
                var probe = new HandlerProbe { Delay = TimeSpan.FromMilliseconds(50) };
                await using var first = ConsumerTestHost.Create(_output, destination, stream, "shared",
                    consumer: c => c.MaxConcurrency(2), probe: new HandlerProbe { Delay = probe.Delay });
                await using var second = ConsumerTestHost.Create(_output, destination, stream, "shared",
                    consumer: c => c.MaxConcurrency(2), probe: new HandlerProbe { Delay = probe.Delay });
                await first.StartAsync();
                await second.StartAsync();
                await Waiter.WaitAsync(() => NatsTestServer.GetConsumerAsync(stream, "shared").Result != null, TimeSpan.FromSeconds(5));

                for (var i = 0; i < 50; i++)
                {
                    await first.PublishAsync(destination, $"work {i}");
                }

                await Waiter.WaitAsync(() => first.Probe.CompletedCount + second.Probe.CompletedCount >= 50, TimeSpan.FromSeconds(20));
                _output.WriteLine($"first: {first.Probe.CompletedCount}, second: {second.Probe.CompletedCount}");

                (first.Probe.CompletedCount + second.Probe.CompletedCount).Should().Be(50);
                first.Probe.CompletedCount.Should().BeGreaterThan(0);
                second.Probe.CompletedCount.Should().BeGreaterThan(0);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Backlog published before start should be processed")]
        public async Task BacklogShouldBeProcessedAfterRestartAsync()
        {
            var (destination, stream) = await NewStreamAsync();
            try
            {
                await using (var seeder = ConsumerTestHost.Create(_output, destination, stream, "backlog"))
                {
                    for (var batch = 0; batch < 10; batch++)
                    {
                        await Task.WhenAll(Enumerable.Range(0, 100).Select(i => seeder.PublishAsync(destination, $"backlog {batch}-{i}")));
                    }
                }

                await using var host = ConsumerTestHost.Create(_output, destination, stream, "backlog",
                    consumer: c => c.MaxConcurrency(50));
                var sw = Stopwatch.StartNew();
                await host.StartAsync();

                while (host.Probe.CompletedCount < 1000 && sw.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(1000);
                    _output.WriteLine($"{sw.Elapsed}: {host.Probe.CompletedCount} handled");
                }
                _output.WriteLine($"1000 messages drained in {sw.Elapsed}");

                host.Probe.CompletedCount.Should().Be(1000);
                host.Probe.MaxObservedConcurrency.Should().BeLessThanOrEqualTo(50, "MaxConcurrency bounds parallel handlers (FR-014)");
                await AssertNothingPendingAsync(stream, "backlog");
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Server restart during consume should not lose messages")]
        public async Task ServerRestartDuringConsumeShouldNotLoseMessagesAsync()
        {
            var container = NatsTestServer.DockerContainer;
            Assert.SkipWhen(string.IsNullOrEmpty(container), "Set Nats:DockerContainer (user secrets) or NATS_DOCKER_CONTAINER to the nats container name to run this test.");

            var (destination, stream) = await NewStreamAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "restart",
                    consumer: c => c.MaxConcurrency(5));
                host.Probe.Delay = TimeSpan.FromMilliseconds(20);
                for (var i = 0; i < 200; i++)
                {
                    await host.PublishAsync(destination, $"restart {i}");
                }
                await host.StartAsync();
                await Waiter.WaitAsync(() => host.Probe.CompletedCount >= 20, TimeSpan.FromSeconds(10));

                using (var restart = Process.Start("docker", $"restart {container}"))
                {
                    await restart!.WaitForExitAsync();
                }

                await Waiter.WaitAsync(() => host.Probe.Completed.Count >= 200, TimeSpan.FromSeconds(60));
                host.Probe.Completed.Count.Should().Be(200);
                host.Probe.Completed.Values.Should().OnlyContain(n => n == 1);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }
    }
}
