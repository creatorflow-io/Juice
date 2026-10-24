using Juice.EventBus.NATS;
using Juice.EventBus.NATS.Consuming;
using Juice.EventBus.NATS.Infrastructure;
using Juice.EventBus.NATS.Policies;
using Juice.EventBus.NATS.Publishing;
using Juice.EventBus.Policies;
using Juice.EventBus.Publishing;
using Juice.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class JetStreamServiceCollectionExtensions
    {
        /// <summary>
        /// Register NATS JetStream connections, producers and consumers
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddJetStream(this EventBusBuilder builder,
            Action<JetStreamEventBusBuilder> configure)
        {
            var jetStreamBuilder = new JetStreamEventBusBuilder(builder);
            configure(jetStreamBuilder);
            return builder;
        }
    }

    public sealed class JetStreamEventBusBuilder
    {
        private readonly EventBusBuilder _eventBus;
        private readonly IServiceCollection _services;
        public MessagingBuilder Messaging => _eventBus.Messaging;
        public EventBusBuilder EventBus => _eventBus;

        internal JetStreamEventBusBuilder(EventBusBuilder eventBus)
        {
            _services = eventBus.Services;
            _eventBus = eventBus;
        }

        /// <summary>
        /// Add a NATS JetStream connection
        /// </summary>
        /// <param name="name"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public JetStreamEventBusBuilder AddConnection(string name, Action<JetStreamConnectionOptions> configure)
        {
            var options = new JetStreamConnectionOptions();
            configure(options);

            _services.AddKeyedSingleton<IJetStreamConnection>(name,
                (sp, serviceKey)
                => CreateConnection(sp, name, options));
            return this;
        }

        /// <summary>
        /// Add a NATS JetStream connection
        /// </summary>
        /// <param name="name"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public JetStreamEventBusBuilder AddConnection(string name, IConfigurationSection configuration)
        {
            var options = new JetStreamConnectionOptions();
            configuration.Bind(options);

            // last registration wins, as for the Action overload
            _services.AddKeyedSingleton<IJetStreamConnection>(name,
                (sp, serviceKey)
                => CreateConnection(sp, name, options));
            return this;
        }

        private readonly HashSet<string> _registeredConsumers = new();
        /// <summary>
        /// Add a JetStream durable pull consumer
        /// </summary>
        /// <param name="key">Subscriptions key and MessageContext source</param>
        /// <param name="stream"></param>
        /// <param name="durableName"></param>
        /// <param name="connectionName"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public JetStreamEventBusBuilder AddConsumer(
            string key,
            string stream, string durableName, string connectionName,
            Action<JetStreamConsumerBuilder>? configure = default)
        {
            _services.TryAddSingleton<IRetryPolicyProvider<JetStreamRetryPolicy>, JetStreamRetryPolicyProvider>();

            if (!_registeredConsumers.Add($"{connectionName}:{stream}:{durableName}"))
            {
                throw new InvalidOperationException($"A consumer '{durableName}' for the stream '{stream}' and connection '{connectionName}' has already been registered.");
            }
            var consumerBuilder = new JetStreamConsumerBuilder(key, stream, durableName, connectionName, _eventBus);
            configure?.Invoke(consumerBuilder);

            // register a hosted service to run the consumer
            _services.AddSingleton<IHostedService>(sp =>
                consumerBuilder.BuildHostedService(sp));

            return this;
        }

        private readonly HashSet<string> _registeredProducers = new();
        /// <summary>
        /// Add a JetStream event producer, registered as a keyed <see cref="ITransportPublisher"/>
        /// </summary>
        /// <param name="key">Publisher key used by publishing policies, conventionally <c>nats</c></param>
        /// <param name="connectionName"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public JetStreamEventBusBuilder AddProducer(
            string key, string connectionName,
            Action<JetStreamProducerBuilder>? configure = default
            )
        {
            var builder = new JetStreamProducerBuilder(key, connectionName);
            configure?.Invoke(builder);
            if (!_registeredProducers.Add(key))
            {
                throw new InvalidOperationException($"A producer with the key '{key}' has already been registered.");
            }
            _services.AddKeyedSingleton<ITransportPublisher>(key, (sp, key) => builder.BuildPublisher(sp));

            return this;
        }

        /// <summary>
        /// Add infrastructure initializer, run by <c>InitJetStreamInfrastructureAsync()</c>.
        /// </summary>
        /// <param name="connectionName"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public JetStreamEventBusBuilder AddInfrastructureTopology(string connectionName, Action<JetStreamInfrastructureBuilder> configure)
        {
            var infrastructureBuilder = new JetStreamInfrastructureBuilder(connectionName);
            configure(infrastructureBuilder);

            _services.AddSingleton(sp => infrastructureBuilder.Build(sp));

            return this;
        }

        private static DefaultJetStreamConnection CreateConnection(IServiceProvider sp, string name, JetStreamConnectionOptions options)
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DefaultJetStreamConnection).FullName + $"[{name}]");
            return new DefaultJetStreamConnection(name, options, logger);
        }
    }
}
