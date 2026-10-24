using System;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.XUnit;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    [InitializeMessageContext]
    public class JetStreamLifecycleTest
    {
        private readonly ITestOutputHelper _output;

        public JetStreamLifecycleTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [IgnoreOnCIFact(DisplayName = "Shutdown should let an in-flight handler finish")]
        public async Task ShutdownShouldLetInFlightHandlerFinishAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.lifecycle");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "graceful");
                host.Probe.Delay = TimeSpan.FromSeconds(1);
                await host.StartAsync();

                var evt = await host.PublishAsync(destination, "slow");
                await Waiter.WaitAsync(() => host.Probe.Attempts.ContainsKey(evt.MessageId), TimeSpan.FromSeconds(5));
                await Task.Delay(200);

                await host.StopAsync(TimeSpan.FromSeconds(10));

                host.Probe.Completed.Should().ContainKey(evt.MessageId);
                await Waiter.WaitAsync(() => NatsTestServer.GetConsumerAsync(stream, "graceful").Result!.NumAckPending == 0, TimeSpan.FromSeconds(5));
                (await NatsTestServer.GetConsumerAsync(stream, "graceful"))!.NumAckPending.Should().Be(0);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Shutdown beyond the grace period should leave the message for redelivery")]
        public async Task ShutdownBeyondGraceShouldLeaveMessageForRedeliveryAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.lifecycle");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            try
            {
                Guid messageId;
                await using (var first = ConsumerTestHost.Create(_output, destination, stream, "abandoned",
                    consumer: c => c.AckWait(TimeSpan.FromSeconds(2))))
                {
                    first.Probe.Delay = TimeSpan.FromSeconds(10);
                    await first.StartAsync();
                    var evt = await first.PublishAsync(destination, "too slow");
                    messageId = evt.MessageId;
                    await Waiter.WaitAsync(() => first.Probe.Attempts.ContainsKey(messageId), TimeSpan.FromSeconds(5));

                    await first.StopAsync(TimeSpan.FromSeconds(1));
                    first.Probe.Completed.Should().NotContainKey(messageId);
                }

                await using var second = ConsumerTestHost.Create(_output, destination, stream, "abandoned",
                    consumer: c => c.AckWait(TimeSpan.FromSeconds(2)));
                await second.StartAsync();

                await Waiter.WaitAsync(() => second.Probe.Completed.ContainsKey(messageId), TimeSpan.FromSeconds(10));
                second.Probe.Completed.Should().ContainKey(messageId);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }
    }
}
