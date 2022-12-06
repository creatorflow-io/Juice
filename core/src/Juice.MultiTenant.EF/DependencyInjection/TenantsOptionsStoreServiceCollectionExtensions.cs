using Juice.Extensions.Options.Stores;
using Juice.MultiTenant.EF.DependencyInjection;
using Juice.MultiTenant.EF.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MultiTenant.DependencyInjection
{
    public static class TenantsOptionsStoreServiceCollectionExtensions
    {
        /// <summary>
        /// Save tenant settings to TenantDbContext
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection UseTenantsOptionsMutableEFStore(this IServiceCollection services, IConfiguration configuration, Action<Juice.EF.DbOptions> configureOptions)
        {
            services.AddTenantSettingsDbContext(configuration, configureOptions);
            services.TryAddScoped<ITenantsOptionsMutableStore, TenantsOptionsMutableEFStore>();
            return services;
        }
    }
}
