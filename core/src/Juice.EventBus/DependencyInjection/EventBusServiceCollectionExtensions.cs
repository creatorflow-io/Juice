using Juice.EventBus;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class EventBusServiceCollectionExtensions
    {
        /// <summary>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="topicSupport"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterInMemoryEventBus(this IServiceCollection services, bool topicSupport = true)
        {
            services.AddIntegrationEventTypesService();

            services.AddSingleton<IEventBusSubscriptionsManager>(sp => {
                var logger = sp.GetRequiredService<ILogger<InMemoryEventBusSubscriptionsManager>>();
                return new InMemoryEventBusSubscriptionsManager(logger, topicSupport);
            });

            services.AddSingleton<IEventBus, InMemoryEventBus>();

            return services;
        }

        /// <summary>
        /// Register in-memory event bus for specific type <typeparamref name="T"/> as <see cref="IEventBus{T}"/> and <typeparamref name="T"/> if <typeparamref name="T"/> implements <see cref="IEventBus"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="topicSupport"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterInMemoryEventBus<T>(this IServiceCollection services, bool topicSupport = true)
        {
            services.TryAddSingleton<IEventBus<T>>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<InMemoryEventBus<T>>();
                var logger1 = loggerFactory.CreateLogger<InMemoryEventBusSubscriptionsManager>();
                var subsManager = new InMemoryEventBusSubscriptionsManager(logger1, topicSupport);
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                return new InMemoryEventBus<T>(subsManager, scopeFactory, logger);
            });
            if (typeof(T).IsAssignableTo(typeof(IEventBus)))
            {
                services.AddEventBusProxy<T>();
            }
            return services;
        }

        /// <summary>
        /// Register integration event types service.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddIntegrationEventTypesService(this IServiceCollection services)
        {
            services.TryAddSingleton<IntegrationEventTypes>();
            return services;
        }

        /// <summary>
        /// Register event bus proxy for specific type.
        /// </summary>
        /// <typeparam name="TBus"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddEventBusProxy<TBus>(this IServiceCollection services)
        {
            if (!typeof(IEventBus).IsAssignableFrom(typeof(TBus)))
            {
                throw new InvalidOperationException($"Type {typeof(TBus).FullName} must implement IEventBus.");
            }
            services.TryAddSingleton(typeof(TBus), sp => EventBusProxy<TBus>.Create(sp.GetRequiredService<IEventBus<TBus>>())!);
            return services;
        }
    }
}
