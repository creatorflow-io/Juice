using Juice.Extensions.Configuration;
using Juice.MultiTenant.EF.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.EF.ConfigurationProviders.DependencyInjection
{
    public static class ConfigurationBuilderExtensions
    {
        /// <summary>
        /// Read tenant settings from TenantDbContext
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantsEntityConfiguration(this IServiceCollection services, IConfiguration configuration, Action<Juice.EF.DbOptions> configureOptions)
        {
            services.AddTenantSettingsDbContext(configuration, configureOptions);
            return services.AddScoped<ITenantsConfigurationSource, EntityConfigurationSource>();
        }


    }
}
