using Juice.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class EventBusServiceCollectionExtensions
    {
        /// <summary>
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterInMemoryEventBus(this IServiceCollection services)
        {


            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            services.AddSingleton<IEventBus, InMemoryEventBus>();

            return services;
        }
    }
}
