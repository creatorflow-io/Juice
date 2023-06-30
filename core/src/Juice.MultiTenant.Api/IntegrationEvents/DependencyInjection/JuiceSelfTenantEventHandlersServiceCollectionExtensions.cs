using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.MultiTenant.Api.IntegrationEvents.Handlers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MultiTenant.Api
{
    public static class JuiceSelfTenantEventHandlersServiceCollectionExtensions
    {
        /// <summary>
        /// Self handle tenant integration events to update tenant DistributedCacheStore. Required DistributedCache.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantIntegrationEventSelfHandlers<TTenantInfo>(this IServiceCollection services)
            where TTenantInfo : class, ITenantInfo, new()
        {
            services.TryAddScoped(sp =>
            {
                var cache = sp.GetRequiredService<IDistributedCache>();
                return new DistributedCacheStore<TTenantInfo>(cache, Constants.TenantToken, default);
            });

            services.AddScoped<TenantActivatedIngtegrationEventSelfHandler<TTenantInfo>>();
            services.AddScoped<TenantDeactivatedIngtegrationEventSelfHandler<TTenantInfo>>();
            services.AddScoped<TenantSuspendedIngtegrationEventSelfHandler<TTenantInfo>>();
            return services;
        }
    }
}
