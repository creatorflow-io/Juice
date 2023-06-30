using Juice.Extensions.Configuration;
using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;
using Juice.MultiTenant.EF.Extensions.Configuration;
using Juice.MultiTenant.EF.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MultiTenant.EF
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

        /// <summary>
        /// Register an <see cref="ITenantsOptionsMutableStore"/> to update tenant settings into DB
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantsOptionsMutableEF(this IServiceCollection services)
        {
            services.AddTenantSettingsOptionsMutableStore();

            services.TryAddScoped<ITenantSettingsRepository, TenantSettingsRepository>();
            return services;
        }

    }
}
