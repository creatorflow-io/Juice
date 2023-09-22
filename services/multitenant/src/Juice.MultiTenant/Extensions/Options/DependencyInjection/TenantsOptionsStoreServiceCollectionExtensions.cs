using Juice.Extensions.Options.Stores;
using Juice.MultiTenant.Extensions.Options.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MultiTenant
{
    public static class TenantsOptionsStoreServiceCollectionExtensions
    {
        /// <summary>
        /// Save tenant settings to TenantSettings, required <see cref="ITenantSettingsRepository"/> service
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantSettingsOptionsMutableStore(this IServiceCollection services)
        {
            services.TryAddScoped<ITenantsOptionsMutableStore, TenantSettingsOptionsMutableStore>();

            return services;
        }

    }
}
