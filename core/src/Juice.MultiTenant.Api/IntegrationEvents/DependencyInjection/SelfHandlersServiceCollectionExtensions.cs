using Finbuckle.MultiTenant.Stores;
using Juice.MultiTenant.Api.IntegrationEvents.Handlers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MultiTenant.Api.IntegrationEvents.DependencyInjection
{
    public static class SelfHandlersServiceCollectionExtensions
    {
        /// <summary>
        /// Self handle tenant integration events to update tenant DistributedCacheStore. Required DistributedCache.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantIntegrationEventSelfHandlers(this IServiceCollection services)
        {
            services.TryAddScoped(sp =>
            {
                var cache = sp.GetRequiredService<IDistributedCache>();
                return new DistributedCacheStore<Tenant>(cache, Constants.TenantToken, default);
            });

            services.AddScoped<TenantActivatedIngtegrationEventSelfHandler>();
            services.AddScoped<TenantDeactivatedIngtegrationEventSelfHandler>();
            return services;
        }
    }
}
