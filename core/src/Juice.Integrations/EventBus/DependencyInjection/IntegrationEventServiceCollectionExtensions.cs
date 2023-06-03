using Juice.Integrations.EventBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Integrations
{
    public static class IntegrationEventServiceCollectionExtensions
    {
        /// <summary>
        /// Add IntegrationEventService factory to create service for SCOPED DbContext
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddIntegrationEventService(this IServiceCollection services)
        {
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IIntegrationEventService<>), typeof(IntegrationEventService<>)));
            return services;
        }
    }
}
