using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Juice.EF.Tests.Events;
using Juice.EventBus.NATS.Consuming;
using Juice.EventBus.NATS.Infrastructure;
using Juice.EventBus.Policies;
using Juice.EventBus.Publishing;
using Juice.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    /// <summary>
    /// A host running one JetStream consumer (plus a producer to seed messages).
    /// </summary>
    internal sealed class ConsumerTestHost : IAsyncDisposable
    {
        public ServiceProvider Services { get; }
        public HandlerProbe Probe => Services.GetRequiredService<HandlerProbe>();
        private IHostedService[] _hostedServices = [];

        private ConsumerTestHost(ServiceProvider services)
        {
            Services = services;
        }

        public static ConsumerTestHost Create(ITestOutputHelper output,
            string destination, string stream, string durable,
            Action<JetStreamConsumerBuilder>? consumer = default,
            Action<ConsumeRetryPolicyOptions>? retry = default,
            Action<IServiceCollection>? configure = default,
            HandlerProbe? probe = default,
            LogLevel logLevel = LogLevel.Information)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddTestOutputLogger(output).SetMinimumLevel(logLevel));
            services.AddDistributedMemoryCache();
            services.AddSingleton(probe ?? new HandlerProbe());
            configure?.Invoke(services);

            services.AddMessaging().AddIdempotencyDistributedCache();
            services.AddEventBus()
                .AddConsumerRetryPolicies(o =>
                {
                    o.Default = new ConsumeRetryPolicyDefinition { RetryTiers = ["1s", "2s"], IsParkingEnabled = true };
                    retry?.Invoke(o);
                })
                .AddJetStream(js => js
                    .AddConnection("nats", ConfigureConnection)
                    // separate consumer connection: on Docker Desktop, publishing and consuming over one
                    // connection stalls ~40 ms per request in the port-forwarding proxy
                    .AddConnection(ConsumerConnection, ConfigureConnection)
                    .AddProducer("nats", "nats")
                    .AddConsumer("nats.xunit", stream, durable, ConsumerConnection, c =>
                    {
                        c.Destination(destination)
                            .Provisioning(JetStreamProvisioning.CreateIfMissing)
                            .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>();
                        consumer?.Invoke(c);
                    }));

            return new ConsumerTestHost(services.BuildServiceProvider());
        }

        public const string ConsumerConnection = "nats-consumer";

        public static void ConfigureConnection(JetStreamConnectionOptions options)
        {
            options.Url = NatsTestServer.Url;
            options.ReconnectWaitMin = TimeSpan.FromMilliseconds(200);
            options.ReconnectWaitMax = TimeSpan.FromSeconds(1);
        }

        public async Task StartAsync()
        {
            _hostedServices = Services.GetServices<IHostedService>().ToArray();
            foreach (var hostedService in _hostedServices)
            {
                await hostedService.StartAsync(CancellationToken.None);
            }
        }

        public async Task StopAsync(TimeSpan? timeout = default)
        {
            foreach (var hostedService in Enumerable.Reverse(_hostedServices))
            {
                using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
                await hostedService.StopAsync(cts.Token);
            }
            _hostedServices = [];
        }

        public JetStreamConsumerHostedService ConsumerService
            => _hostedServices.OfType<JetStreamConsumerHostedService>().Single();

        public async Task<ContentPublishedIntegrationEvent> PublishAsync(string destination, string message,
            string? routingKey = default, string? tenantId = default, string? correlationId = default)
        {
            var evt = new ContentPublishedIntegrationEvent(message);
            await PublishAsync(destination, evt, routingKey, tenantId, correlationId);
            return evt;
        }

        public async Task PublishAsync(string destination, ContentPublishedIntegrationEvent evt,
            string? routingKey = default, string? tenantId = default, string? correlationId = default)
        {
            var serializer = Services.GetRequiredService<IMessageSerializer>();
            var publisher = Services.GetRequiredKeyedService<ITransportPublisher>("nats");
            var headers = Headers(evt, tenantId, correlationId);
            await publisher.PublishAsync(serializer.SerializeToUtf8Bytes(evt),
                new PublishContext(evt.MessageId.ToString(), destination, tenantId, headers, routingKey));
        }

        public static Dictionary<string, object?> Headers(ContentPublishedIntegrationEvent evt,
            string? tenantId = default, string? correlationId = default)
            => new()
            {
                ["x-message-id"] = evt.MessageId.ToString(),
                ["x-message-type"] = nameof(ContentPublishedIntegrationEvent),
                ["x-message-name"] = nameof(ContentPublishedIntegrationEvent),
                ["x-correlation-id"] = correlationId ?? Guid.NewGuid().ToString(),
                ["x-tenant-id"] = tenantId
            };

        public byte[] Serialize(ContentPublishedIntegrationEvent evt)
            => Services.GetRequiredService<IMessageSerializer>().SerializeToUtf8Bytes(evt);

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            await Services.DisposeAsync();
        }
    }
}
