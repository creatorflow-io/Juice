using Juice.Extensions.Options.Stores;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TenantsOptionsStoreServiceCollectionExtensions
    {
        /// <summary>
        /// Save per-tenant options to tenants/[Tenant name]/[file.json]
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection UseTenantsOptionsMutableFileStore(this IServiceCollection services, string file)
        {
            services.TryAddTransient<ITenantsOptionsMutableStore>(sp =>
            {
                var tenant = sp.GetRequiredService<ITenantAccessor>().Tenant;
                if (tenant == null)
                {
                    throw new InvalidOperationException("Tenant is not available");
                }
                return new TenantsOptionsMutableJsonFileStore(tenant, file);
            });
            return services;
        }

        /// <summary>
        /// Save per-tenant options to tenants/[Tenant name]/[custome file.json] for specified input type
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection UseTenantsOptionsMutableFileStore<T>(this IServiceCollection services, string file)
        {
            services.TryAddTransient<ITenantsOptionsMutableStore<T>>(sp =>
            {
                var tenant = sp.GetRequiredService<ITenantAccessor>().Tenant;
                if (tenant == null)
                {
                    throw new InvalidOperationException("Tenant is not available");
                }
                return new TenantsOptionsMutableFileStore<T>(tenant, file);
            });
            return services;
        }
    }
}
