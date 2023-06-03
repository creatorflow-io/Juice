using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.EventBus
{
    public static class EventBusServiceCollectionExtensions
    {
        /// <summary>
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterInMemoryEventBus(this IServiceCollection services)
        {
            services.AddIntegrationEventTypesService();

            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            services.AddSingleton<IEventBus, InMemoryEventBus>();

            return services;
        }

        public static IServiceCollection AddIntegrationEventTypesService(this IServiceCollection services)
        {
            services.TryAddSingleton<IntegrationEventTypes>();
            return services;
        }
    }
}
