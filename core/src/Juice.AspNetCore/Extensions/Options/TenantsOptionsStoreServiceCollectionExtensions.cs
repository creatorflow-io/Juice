using Juice.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Extensions.Options
{
    public static class TenantsOptionsStoreServiceCollectionExtensions
    {
        public static IServiceCollection UseTenantsOptionsMutableFileStore(this IServiceCollection services, string file)
        {
            services.TryAddTransient<ITenantsOptionsMutableStore>(sp =>
            {
                var tenant = sp.GetRequiredService<ITenant>();
                return new TenantsOptionsMutableFileStore(tenant, file);
            });
            return services;
        }

        public static IServiceCollection UseTenantsOptionsMutableFileStore<T>(this IServiceCollection services, string file)
        {
            services.TryAddTransient<ITenantsOptionsMutableStore<T>>(sp =>
            {
                var tenant = sp.GetRequiredService<ITenant>();
                return new TenantsOptionsMutableFileStore<T>(tenant, file);
            });
            return services;
        }
    }
}
