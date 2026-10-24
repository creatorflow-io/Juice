using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Tests.Events;
using Juice.EventBus.Publishing;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    [InitializeMessageContext]
    public class JetStreamPublishTest
    {
        private readonly ITestOutputHelper _output;

        public JetStreamPublishTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private ServiceProvider BuildServices(string url = NatsTestServer.Url)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddTestOutputLogger(_output).SetMinimumLevel(LogLevel.Debug));
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o =>
                    {
                        o.Url = url;
                        o.ConnectTimeout = TimeSpan.FromSeconds(1);
                        o.MaxReconnectRetry = 0;
                    })
                    .AddProducer("nats", "nats", p => p.PublishTimeout(TimeSpan.FromSeconds(3))));
            return services.BuildServiceProvider();
        }

        private static Dictionary<string, object?> StandardHeaders(string messageId, string? tenantId = "tenant-a")
            => new()
            {
                ["x-message-id"] = messageId,
                ["x-message-type"] = nameof(ContentPublishedIntegrationEvent),
                ["x-message-name"] = nameof(ContentPublishedIntegrationEvent),
                ["x-correlation-id"] = "corr-1",
                ["x-causation-id"] = "cause-1",
                ["x-source"] = "xunit",
                ["x-tenant-id"] = tenantId
            };

        [IgnoreOnCIFact(DisplayName = "Publish should store the message with its headers")]
        public async Task PublishShouldStoreMessageWithHeadersAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.publish");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            try
            {
                using var sp = BuildServices();
                var publisher = sp.GetRequiredKeyedService<ITransportPublisher>("nats");
                var messageId = Guid.NewGuid().ToString();
                var payload = Encoding.UTF8.GetBytes("{\"Message\":\"hello\"}");

                await publisher.PublishAsync(payload,
                    new PublishContext(messageId, destination, "tenant-a", StandardHeaders(messageId)));

                var stored = await NatsTestServer.ReadAllAsync(stream);
                var message = stored.Should().ContainSingle().Subject;
                message.Subject.Should().Be($"{destination}.{nameof(ContentPublishedIntegrationEvent)}");
                message.Data.Should().Equal(payload);
                foreach (var header in StandardHeaders(messageId))
                {
                    message.Headers.Should().ContainKey(header.Key).WhoseValue.Should().Be(header.Value);
                }
                message.Headers["x-original-exchange"].Should().Be(destination);
                message.Headers["x-original-routing-key"].Should().Be(nameof(ContentPublishedIntegrationEvent));
                message.Headers["Nats-Msg-Id"].Should().Be($"{messageId}:{message.Subject}");
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Publishing the same message id twice should store it once")]
        public async Task PublishSameMessageIdTwiceShouldStoreOnceAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.publish");
            var stream = JetStreamNaming.DeriveStreamName(destination);
            await NatsTestServer.EnsureStreamAsync(stream, destination + ".>");
            try
            {
                using var sp = BuildServices();
                var publisher = sp.GetRequiredKeyedService<ITransportPublisher>("nats");
                var messageId = Guid.NewGuid().ToString();
                var payload = Encoding.UTF8.GetBytes("{}");

                await publisher.PublishAsync(payload, new PublishContext(messageId, destination, Headers: StandardHeaders(messageId)));
                await publisher.PublishAsync(payload, new PublishContext(messageId, destination, Headers: StandardHeaders(messageId)));

                (await NatsTestServer.ReadAllAsync(stream)).Should().ContainSingle();
            }
            finally
            {
                await NatsTestServer.DeleteStreamAsync(stream);
            }
        }

        [IgnoreOnCIFact(DisplayName = "Publishing without a stream should throw a clear error")]
        public async Task PublishWithoutStreamShouldThrowClearErrorAsync()
        {
            var destination = NatsTestServer.UniqueDestination("xunit.nostream");
            using var sp = BuildServices();
            var publisher = sp.GetRequiredKeyedService<ITransportPublisher>("nats");
            var messageId = Guid.NewGuid().ToString();

            var act = async () => await publisher.PublishAsync(Encoding.UTF8.GetBytes("{}"),
                new PublishContext(messageId, destination, Headers: StandardHeaders(messageId)));

            var ex = await act.Should().ThrowAsync<InvalidOperationException>();
            ex.Which.Message.Should().Contain($"{destination}.{nameof(ContentPublishedIntegrationEvent)}")
                .And.Contain($"destination '{destination}'");
        }

        [IgnoreOnCIFact(DisplayName = "Publishing to an unreachable server should throw")]
        public async Task PublishWhenServerUnreachableShouldThrowAsync()
        {
            using var sp = BuildServices("nats://127.0.0.1:4299");
            var publisher = sp.GetRequiredKeyedService<ITransportPublisher>("nats");
            var messageId = Guid.NewGuid().ToString();

            var act = async () => await publisher.PublishAsync(Encoding.UTF8.GetBytes("{}"),
                new PublishContext(messageId, "xunit.unreachable", Headers: StandardHeaders(messageId)));

            await act.Should().ThrowAsync<Exception>();
        }
    }
}
