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
                if(services.Any(s => s.ServiceType == typeof(IEventBus)))
                {
                    throw new InvalidOperationException("The default service of IEventBus is already registered. Please check your service registrations.");
                }
                services.AddSingleton<IEventBus>(sp => {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<RabbitMQEventBus>();
                    var logger1 = loggerFactory.CreateLogger<InMemoryEventBusSubscriptionsManager>();
                    var logger2 = loggerFactory.CreateLogger<DefaultRabbitMQPersistentConnection>();
                    var subsManager = new InMemoryEventBusSubscriptionsManager(logger1, options.ExchangeType == "topic");
                    var connection = new DefaultRabbitMQPersistentConnection(options, logger2);
                    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                    logger.LogDebug("RabbitMQ EventBus created with BrokerName: {BrokerName}, HostName: {HostName}, Port: {Port}, VirtualHost: {VirtualHost}, ExchangeType: {ExchangeType}, User: {User}",
                        options.BrokerName, options.Connection, options.Port, options.VirtualHost, options.ExchangeType, options.UserName);
                    return new RabbitMQEventBus(subsManager, scopeFactory, logger, connection, options);
                });

                services.AddIntegrationEventTypesService();
            }
            return services;

        }

        /// <summary>
        /// Register RabbitMQ service of type <c>T</c> as <see cref="IEventBus{T}"/> and <typeparamref name="T"/> if <typeparamref name="T"/> implements <see cref="IEventBus"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterRabbitMQEventBus<T>(this IServiceCollection services, IConfiguration configuration, Action<RabbitMQOptions>? configure = null)
        {
            var enabled = configuration.GetValue<bool>(nameof(RabbitMQOptions.RabbitMQEnabled));
            if (enabled)
            {
                var options = new RabbitMQOptions();
                configuration.Bind(options);
                configure?.Invoke(options);

                services.TryAddSingleton<IEventBus<T>>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<RabbitMQEventBus<T>>();
                    var logger1 = loggerFactory.CreateLogger<InMemoryEventBusSubscriptionsManager>();
                    var logger2 = loggerFactory.CreateLogger<DefaultRabbitMQPersistentConnection>();
                    var subsManager = new InMemoryEventBusSubscriptionsManager(logger1, options.ExchangeType == "topic");
                    var connection = new DefaultRabbitMQPersistentConnection(options, logger2);
                    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                    return new RabbitMQEventBus<T>(subsManager, scopeFactory, logger, connection, options);
                });

                if(typeof(T).IsAssignableTo(typeof(IEventBus)))
                {
                    services.AddEventBusProxy<T>();
                }

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

                services.TryAddKeyedSingleton<IEventBus>(options.BrokerName, (sp, key) => {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger(typeof(RabbitMQEventBus).Name + "_" + key);
                    var logger1 = loggerFactory.CreateLogger<InMemoryEventBusSubscriptionsManager>();
                    var logger2 = loggerFactory.CreateLogger<DefaultRabbitMQPersistentConnection>();
                    var subsManager = new InMemoryEventBusSubscriptionsManager(logger1, options.ExchangeType == "topic");
                    var connection = new DefaultRabbitMQPersistentConnection(options, logger2);
                    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                    return new RabbitMQEventBus(subsManager, scopeFactory, logger, connection, options);
                });

                services.AddIntegrationEventTypesService();
            }
            return services;

        }
    }
}
