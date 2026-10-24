using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.XUnit;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    [InitializeMessageContext]
    public class JetStreamRetryParkingTest
    {
        private readonly ITestOutputHelper _output;

        public JetStreamRetryParkingTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private static async Task<(string Destination, string Stream, string Parking)> NewStreamsAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.retry");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            var parking = JetStreamNaming.ParkingStreamName(stream);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            await NatsTestServer.EnsureStreamAsync(parking, $"parking.{destination}.>");
            return (destination, stream, parking);
        }

        private static async Task CleanupAsync(string stream, string parking)
        {
            await NatsTestServer.DeleteStreamAsync(stream);
            await NatsTestServer.DeleteStreamAsync(parking);
        }

        [IgnoreOnCIFact(DisplayName = "Handler failing twice should succeed on the third attempt")]
        public async Task HandlerFailingTwiceShouldSucceedOnThirdAttemptAsync()
        {
            var (destination, stream, parking) = await NewStreamsAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "retry");
                host.Probe.FailuresByMessage["flaky"] = 2;
                await host.StartAsync();

                var evt = await host.PublishAsync(destination, "flaky");

                await Waiter.WaitAsync(() => host.Probe.Completed.ContainsKey(evt.MessageId), TimeSpan.FromSeconds(10));

                var attempts = host.Probe.Calls.Where(c => c.MessageId == evt.MessageId).Select(c => c.At).ToArray();
                attempts.Should().HaveCount(3);
                var first = attempts[1] - attempts[0];
                var second = attempts[2] - attempts[1];
                _output.WriteLine($"Gaps: {first.TotalMilliseconds} ms, {second.TotalMilliseconds} ms");
                first.TotalMilliseconds.Should().BeInRange(800, 1500);
                second.TotalMilliseconds.Should().BeInRange(1600, 2600);
                (await NatsTestServer.ReadAllAsync(parking)).Should().BeEmpty();
            }
            finally
            {
                await CleanupAsync(stream, parking);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Always failing handler should be parked with failure metadata")]
        public async Task AlwaysFailingHandlerShouldBeParkedAsync()
        {
            var (destination, stream, parking) = await NewStreamsAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "retry");
                host.Probe.FailuresByMessage["poison"] = -1;
                await host.StartAsync();

                var evt = await host.PublishAsync(destination, "poison");

                await Waiter.WaitAsync(() => NatsTestServer.ReadAllAsync(parking).Result.Count >= 1, TimeSpan.FromSeconds(10));

                var parked = (await NatsTestServer.ReadAllAsync(parking)).Should().ContainSingle().Subject;
                parked.Subject.Should().Be($"parking.{destination}.ContentPublishedIntegrationEvent");
                parked.Headers["x-message-id"].Should().Be(evt.MessageId.ToString());
                parked.Headers["x-attempts"].Should().Be("3");
                parked.Headers["x-death-reason"].Should().Be("MaxRetriesReached_Attempts_3");
                parked.Headers["x-original-queue"].Should().Be("retry");
                parked.Headers["x-original-exchange"].Should().Be(destination);
                host.Probe.Attempts[evt.MessageId].Should().Be(3);

                await Waiter.WaitAsync(() => NatsTestServer.GetConsumerAsync(stream, "retry").Result!.NumAckPending == 0, TimeSpan.FromSeconds(5));
                var consumer = await NatsTestServer.GetConsumerAsync(stream, "retry");
                consumer!.NumAckPending.Should().Be(0);
                consumer.NumPending.Should().Be(0UL);
            }
            finally
            {
                await CleanupAsync(stream, parking);
            }
        }

        [IgnoreOnCIFact(DisplayName = "With parking disabled the message should be terminated")]
        public async Task ParkingDisabledShouldTerminateAsync()
        {
            var (destination, stream, parking) = await NewStreamsAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "retry",
                    retry: o => o.Default!.IsParkingEnabled = false);
                host.Probe.FailuresByMessage["poison"] = -1;
                await host.StartAsync();

                var evt = await host.PublishAsync(destination, "poison");

                await Waiter.WaitAsync(() => host.Probe.Attempts.TryGetValue(evt.MessageId, out var n) && n >= 3, TimeSpan.FromSeconds(10));
                await Task.Delay(1000);

                host.Probe.Attempts[evt.MessageId].Should().Be(3);
                (await NatsTestServer.ReadAllAsync(parking)).Should().BeEmpty();
                var consumer = await NatsTestServer.GetConsumerAsync(stream, "retry");
                consumer!.NumAckPending.Should().Be(0);
            }
            finally
            {
                await CleanupAsync(stream, parking);
            }
        }

        [IgnoreOnCIFact(DisplayName = "A failing message should not block other messages")]
        public async Task FailingMessageShouldNotBlockOthersAsync()
        {
            var (destination, stream, parking) = await NewStreamsAsync();
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "retry",
                    consumer: c => c.MaxConcurrency(2));
                host.Probe.FailuresByMessage["poison"] = -1;
                await host.StartAsync();

                var poison = await host.PublishAsync(destination, "poison");
                for (var i = 0; i < 20; i++)
                {
                    await host.PublishAsync(destination, $"good {i}");
                }

                await Waiter.WaitAsync(() => NatsTestServer.ReadAllAsync(parking).Result.Count >= 1, TimeSpan.FromSeconds(10));

                var good = host.Probe.Calls.Where(c => c.Message!.StartsWith("good")).ToArray();
                good.Select(c => c.MessageId).Distinct().Should().HaveCount(20);
                var lastPoisonAttempt = host.Probe.Calls.Where(c => c.MessageId == poison.MessageId).Max(c => c.At);
                good.Max(c => c.At).Should().BeBefore(lastPoisonAttempt);
            }
            finally
            {
                await CleanupAsync(stream, parking);
            }
        }
    }
}
