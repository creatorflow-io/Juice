using Juice.EventBus.RabbitMQ;
using Juice.EventBus.RabbitMQ.Consuming;
using Juice.EventBus.RabbitMQ.Infrastructure;
using Juice.EventBus.RabbitMQ.Policies;
using Juice.EventBus.RabbitMQ.Publishing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RabbitMQServiceCollectionExtensions
    {
        /// <summary>
        /// Register RabbitMQ Event Publisher
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddRabbitMQ(this EventBusBuilder builder,
            Action<RabbitMQEventBusBuilder> configure)
        {
            var rabbitMQBuilder = new RabbitMQEventBusBuilder(builder);
            configure(rabbitMQBuilder);
            return builder;
        }
    }

    public sealed class RabbitMQEventBusBuilder
    {
        private readonly EventBusBuilder _eventBus;
        private readonly IServiceCollection _services;
        private readonly HashSet<string> _registeredQueues = new();

        internal RabbitMQEventBusBuilder(EventBusBuilder eventBus)
        {
            _services = eventBus.Services;
            _eventBus = eventBus;
        }

        /// <summary>
        /// Add a RabbitMQ connection
        /// </summary>
        /// <param name="name"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public RabbitMQEventBusBuilder AddConnection(string name, Action<RabbitMQConnectionOptions> configure)
        {
            var endpoint = new RabbitMQConnectionOptions();
            configure(endpoint);

            _services.AddKeyedSingleton<IRabbitMQPersistentConnection>(name,
                (sp, serviceKey)
                =>
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DefaultRabbitMQPersistentConnection).FullName + $"[{name}]");
                    return new DefaultRabbitMQPersistentConnection(name, endpoint, logger);
                });
            return this;
        }

        /// <summary>
        /// Add a RabbitMQ connection
        /// </summary>
        /// <param name="name"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public RabbitMQEventBusBuilder AddConnection(string name, IConfigurationSection configuration)
        {
            var endpoint = new RabbitMQConnectionOptions();
            configuration.Bind(endpoint);

            _services.TryAddKeyedSingleton<IRabbitMQPersistentConnection>(name,
               (sp, serviceKey)
               =>
               {
                   var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DefaultRabbitMQPersistentConnection).FullName + $"[{name}]");
                   return new DefaultRabbitMQPersistentConnection(name, endpoint, logger);
               });
            return this;
        }

        public RabbitMQEventBusBuilder AddRetryPolicies(IConfigurationSection policies)
        {
            _services.Configure<RetryPolicyOptions>(policies);
            _services.TryAddSingleton<IRetryPolicyProvider, DefaultRetryPolicyProvider>();
            return this;
        }

        /// <summary>
        /// Add a RabbitMQ event consumer
        /// </summary>
        /// <param name="connectionName"></param>
        /// <param name="queueName"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public RabbitMQEventBusBuilder AddConsumer(
             string queueName, string connectionName,
            Action<RabbitMQConsumerBuilder>? configure = default)
        {
            if(!_registeredQueues.Add($"{connectionName}:{queueName}"))
            {
                throw new InvalidOperationException($"A consumer for the queue '{queueName}' and connection '{connectionName}' has already been registered.");
            }
            var queueBuilder = new RabbitMQConsumerBuilder(connectionName, queueName, _eventBus);
            configure?.Invoke(queueBuilder);

            // register a hosted service to run the queue consumer
            _services.AddSingleton<IHostedService>(sp =>
                queueBuilder.BuildHostedService(sp));

            return this;
        }

        private readonly HashSet<string> _registeredProducers = new();
        /// <summary>
        /// Add a RabbitMQ event producer
        /// </summary>
        /// <param name="key"></param>
        /// <param name="connectionName"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public RabbitMQEventBusBuilder AddProducer(
            string key, string connectionName,
            Action<RabbitMQProducerBuilder>? configure = default
            )
        {
            var builder = new RabbitMQProducerBuilder(key, connectionName);
            configure?.Invoke(builder);
            if (!_registeredProducers.Add(key))
            {
                throw new InvalidOperationException($"A producer with the key '{key}' has already been registered.");
            }
            _services.AddKeyedSingleton(key, (sp, key) => builder.BuildPublisher(sp));

            return this;
        }

        /// <summary>
        /// Add infrastructure initializer.
        /// </summary>
        /// <param name="connectionName"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public RabbitMQEventBusBuilder AddInfrastructureTopology(string connectionName, Action<RabbitMQInfrastructureBuilder> configure)
        {
            var infrastructureBuilder = new RabbitMQInfrastructureBuilder(connectionName);
            configure(infrastructureBuilder);

            _services.AddSingleton(sp => infrastructureBuilder.Build(sp));

            return this;
        }
    }
}
