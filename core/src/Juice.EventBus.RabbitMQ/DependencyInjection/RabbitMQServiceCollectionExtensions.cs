using Juice.EventBus;
using Juice.EventBus.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RabbitMQServiceCollectionExtensions
    {
        /// <summary>
        /// Register RabbitMQ Event Bus
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterRabbitMQEventBus(this IServiceCollection services, IConfiguration configuration, Action<RabbitMQOptions>? configure = null)
        {
            var enabled = configuration.GetValue<bool>(nameof(RabbitMQOptions.RabbitMQEnabled));
            if (enabled)
            {
                var options = new RabbitMQOptions();
                configuration.Bind(options);
                configure?.Invoke(options);

                services.TryAddSingleton<IEventBusSubscriptionsManager>(sp => {
                    var logger = sp.GetRequiredService<ILogger<InMemoryEventBusSubscriptionsManager>>();
                    return new InMemoryEventBusSubscriptionsManager(logger, options.ExchangeType == "topic");
                });

                services.TryAddSingleton<IRabbitMQPersistentConnection>(sp=>
                {
                    var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();
                    return new DefaultRabbitMQPersistentConnection(options, logger);
                });

                services.TryAddSingleton<IEventBus>(sp => {
                    var subsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                    var logger = sp.GetRequiredService<ILogger<RabbitMQEventBus>>();
                    var connection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                    return new RabbitMQEventBus(subsManager, scopeFactory, logger, connection, options);
                });

                services.AddIntegrationEventTypesService();
            }
            return services;

        }

        /// <summary>
        /// Register Keyed RabbitMQ Event Bus, useful for working with difference brokers
        /// <para>BrokerName is used to identify the broker and should throw if it's empty</para>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterKeyedRabbitMQEventBus(this IServiceCollection services, IConfiguration? configuration, Action<RabbitMQOptions>? configure = null)
        {
            var enabled = configuration?.GetValue<bool>(nameof(RabbitMQOptions.RabbitMQEnabled))?? true;
            if (enabled)
            {
                var options = new RabbitMQOptions();
                configuration?.Bind(options);
                configure?.Invoke(options);

                ArgumentNullException.ThrowIfNull(options.BrokerName, nameof(options.BrokerName));

                services.TryAddKeyedSingleton<IEventBusSubscriptionsManager>(options.BrokerName, (sp, key) => {
                    var logger = sp.GetRequiredService<ILoggerFactory>();
                    return new InMemoryEventBusSubscriptionsManager(logger.CreateLogger(typeof(InMemoryEventBusSubscriptionsManager).Name + "_" + key),
                        options.ExchangeType == "topic");
                });

                services.TryAddKeyedSingleton<IRabbitMQPersistentConnection>(options.BrokerName, (sp, key) => {
                    var logger = sp.GetRequiredService<ILoggerFactory>();
                    return new DefaultRabbitMQPersistentConnection(options, logger.CreateLogger(typeof(DefaultRabbitMQPersistentConnection).Name + "_" + key));
                });

                services.TryAddKeyedSingleton<IEventBus>(options.BrokerName, (sp, key) => {
                    var subsManager = sp.GetRequiredKeyedService<IEventBusSubscriptionsManager>(key);
                    var logger = sp.GetRequiredService<ILoggerFactory>();
                    var connection = sp.GetRequiredKeyedService<IRabbitMQPersistentConnection>(key);
                    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                    return new RabbitMQEventBus(subsManager, scopeFactory,
                        logger.CreateLogger(typeof(RabbitMQEventBus).Name + "_" + key),
                        connection, options);
                });

                services.AddIntegrationEventTypesService();
            }
            return services;

        }
    }
}
