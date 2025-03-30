using Juice.Extensions.Options.Stores;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TenantOptionsStoreServiceCollectionExtensions
    {
        /// <summary>
        /// Save per-tenant options to tenants/[Tenant name]/[file.json]
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection UseTenantOptionsMutableFileStore(this IServiceCollection services, string file)
        {
            services.TryAddSingleton<IOptionsMutableStore>(sp =>
            {
                return new TenantOptionsMutableJsonFileStore(sp.GetRequiredService<ITenantAccessor>(), file);
            });
            return services;
        }

        /// <summary>
        /// Save per-tenant options to tenants/[Tenant name]/[custome file.json] for specified input type
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection UseTenantOptionsMutableFileStore<T>(this IServiceCollection services, string file)
        {
            services.TryAddSingleton<IOptionsMutableStore<T>>(sp =>
            {
                return new TenantOptionsMutableJsonFileStore<T>(sp.GetRequiredService<ITenantAccessor>(), file);
            });
            return services;
        }
    }
}
