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
        /// Deprecated: We are using EventBusProxy instead, so you do not need to register this wrapper anymore.
        /// <para></para>Register event bus wrapper for specific type.
        /// <para>Consider using keyed service instead on net8.0+</para>
        /// </summary>
        /// <typeparam name="TBus"></typeparam>
        /// <typeparam name="TWrapper"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        [Obsolete("This method will be removed in future versions.")]
        public static IServiceCollection AddEventBusWrapper<TBus, TWrapper>(this IServiceCollection services)
            where TBus : IEventBus
            where TWrapper : EventBusWrapper, TBus
        {
            services.TryAddSingleton(typeof(TBus), typeof(TWrapper));
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
