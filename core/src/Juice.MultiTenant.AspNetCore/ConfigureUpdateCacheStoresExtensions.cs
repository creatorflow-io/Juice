using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.AspNetCore
{
    public static class ConfigureUpdateCacheStoresExtensions
    {
        /// <summary>
        /// Update distributed cache store when tenant resolved. Required DistributedCache.
        /// </summary>
        /// <typeparam name="TTenantInfo"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> ShouldUpdateCacheStore<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder)
            where TTenantInfo : class, ITenant, ITenantInfo, new()
        {

            builder.Services.Configure<MultiTenantOptions>(options =>
            {
                var originEvent = options.Events.OnTenantResolved;

                options.Events.OnTenantResolved = async context =>
                {
                    if (originEvent != null)
                    {
                        await originEvent(context);
                    }

                    if (context.Context is HttpContext httpContext && context.TenantInfo is TTenantInfo tenantInfo)
                    {
                        if (!(context.StoreType?.IsAssignableTo(typeof(DistributedCacheStore<TTenantInfo>)) ?? false))
                        {
                            var cacheStore = httpContext.RequestServices.GetService<DistributedCacheStore<TTenantInfo>>();
                            if (cacheStore != null)
                            {
                                await cacheStore.TryAddAsync(tenantInfo);
                            }
                        }
                    }

                };

            });
            return builder;
        }

    }
}
