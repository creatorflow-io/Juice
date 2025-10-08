using Juice.EventBus;
using Juice.Integrations.EventBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
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
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IIntegrationEventService<,>),typeof(IntegrationEventService<,>)));
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IIntegrationEventService<>), typeof(IntegrationEventService<>)));
            return services;
        }
    }
}
