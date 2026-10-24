using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EventBus.NATS.Infrastructure;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    [InitializeMessageContext]
    public class JetStreamProvisioningTest
    {
        private readonly ITestOutputHelper _output;

        public JetStreamProvisioningTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private ServiceProvider BuildServices(Action<JetStreamInfrastructureBuilder> topology,
            string url = NatsTestServer.Url, Action<JetStreamConnectionOptions>? connection = default)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddTestOutputLogger(_output));
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o =>
                    {
                        o.Url = url;
                        connection?.Invoke(o);
                    })
                    .AddInfrastructureTopology("nats", topology));
            return services.BuildServiceProvider();
        }

        [IgnoreOnCIFact(DisplayName = "CreateIfMissing should create the streams and the consumer")]
        public async Task CreateIfMissingShouldCreateStreamsAndConsumerAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.provision");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            var parking = JetStreamNaming.ParkingStreamName(stream);
            try
            {
                using (var sp = BuildServices(t => t
                    .Provisioning(JetStreamProvisioning.CreateIfMissing)
                    .DeclareStream(destination)
                    .DeclareParkingStream(destination)))
                {
                    await sp.InitJetStreamInfrastructureAsync();
                }

                var main = await NatsTestServer.GetStreamAsync(stream);
                main.Should().NotBeNull();
                main!.Config.Subjects.Should().Equal($"{destination}.>");
                main.Config.DuplicateWindow.Should().Be(TimeSpan.FromMinutes(10));
                var parked = await NatsTestServer.GetStreamAsync(parking);
                parked!.Config.Subjects.Should().Equal($"parking.{destination}.>");
                parked.Config.MaxAge.Should().Be(TimeSpan.FromDays(14));

                await using var host = ConsumerTestHost.Create(_output, destination, stream, "provisioned");
                await host.StartAsync();
                await Waiter.WaitAsync(() => NatsTestServer.GetConsumerAsync(stream, "provisioned").Result != null, TimeSpan.FromSeconds(5));
                var consumer = await NatsTestServer.GetConsumerAsync(stream, "provisioned");
                consumer!.Config.AckPolicy.Should().Be(ConsumerConfigAckPolicy.Explicit);
                consumer.Config.MaxAckPending.Should().Be(20);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
                await NatsTestServer.DeleteStreamAsync(parking);
            }
        }

        [IgnoreOnCIFact(DisplayName = "VerifyOnly should throw one error listing every missing stream")]
        public async Task VerifyOnlyShouldThrowListingMissingItemsAsync()
        {
            var first = NatsTestServer.UniqueDestination("xunit.verify");
            var second = NatsTestServer.UniqueDestination("xunit.verify");
            using var sp = BuildServices(t => t.DeclareStream(first).DeclareStream(second).DeclareParkingStream(first));

            var act = () => sp.InitJetStreamInfrastructureAsync();

            var ex = await act.Should().ThrowAsync<InvalidOperationException>();
            ex.Which.Message.Should()
                .Contain(JetStreamNaming.DeriveStreamName(first))
                .And.Contain(JetStreamNaming.DeriveStreamName(second))
                .And.Contain(JetStreamNaming.ParkingStreamName(JetStreamNaming.DeriveStreamName(first)));
            (await NatsTestServer.GetStreamAsync(JetStreamNaming.DeriveStreamName(first))).Should().BeNull();
        }

        [IgnoreOnCIFact(DisplayName = "An existing stream should be reused unchanged")]
        public async Task ExistingStreamShouldBeReusedUnchangedAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.existing");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            try
            {
                await using (var connection = await NatsTestServer.ConnectAsync())
                {
                    var js = new NatsJSContext(connection);
                    await js.CreateStreamAsync(new StreamConfig(stream, [$"{destination}.>"]) { MaxAge = TimeSpan.FromHours(1) });
                    await js.PublishAsync($"{destination}.Kept", Encoding.UTF8.GetBytes("{}"));
                }

                using (var sp = BuildServices(t => t
                    .Provisioning(JetStreamProvisioning.CreateIfMissing)
                    .DeclareStream(destination, s => s.MaxAge = TimeSpan.FromDays(7))))
                {
                    await sp.InitJetStreamInfrastructureAsync();
                }

                var info = await NatsTestServer.GetStreamAsync(stream);
                info!.Config.MaxAge.Should().Be(TimeSpan.FromHours(1));
                info.State.Messages.Should().Be(1);
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Consumer host with VerifyOnly and a missing consumer should fail to start")]
        public async Task ConsumerHostWithVerifyOnlyAndMissingConsumerShouldFailStartAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.verify");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            try
            {
                await using var host = ConsumerTestHost.Create(_output, destination, stream, "missing",
                    consumer: c => c.Provisioning(JetStreamProvisioning.VerifyOnly));
                await host.StartAsync();

                var execute = host.ConsumerService.ExecuteTask!;
                await Waiter.WaitAsync(() => execute.IsCompleted, TimeSpan.FromSeconds(5));

                execute.IsFaulted.Should().BeTrue();
                execute.Exception!.InnerException.Should().BeAssignableTo<InvalidOperationException>()
                    .Which.Message.Should().Contain("'missing'").And.Contain($"'{stream}'").And.Contain("VerifyOnly");
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "User/password authentication should connect")]
        public async Task UserPasswordAuthShouldConnectAsync()
        {
            var url = TestSettings.AuthUrl;
            Assert.SkipWhen(string.IsNullOrEmpty(url), "Set Nats:Auth:Url/UserName/Password (user secrets) or NATS_AUTH_URL/NATS_AUTH_USER/NATS_AUTH_PASSWORD to run this test.");

            var destination = NatsTestServer.UniqueDestination("xunit.auth");
            using var sp = BuildServices(t => t.Provisioning(JetStreamProvisioning.CreateIfMissing).DeclareStream(destination),
                url!, o =>
                {
                    o.UserName = TestSettings.AuthUserName;
                    o.Password = TestSettings.AuthPassword;
                });

            await sp.InitJetStreamInfrastructureAsync();
            sp.GetRequiredKeyedService<IJetStreamConnection>("nats").State.Should().Be(global::NATS.Client.Core.NatsConnectionState.Open);
            await sp.GetRequiredKeyedService<IJetStreamConnection>("nats").JetStream.DeleteStreamAsync(JetStreamNaming.DeriveStreamName(destination));
        }
    }
}
