using Juice.Extensions.Configuration;
using Juice.MultiTenant.Settings.Grpc;
using Juice.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.MultiTenant.Grpc.Extensions.Configuration.DependencyInjection
{
    public static class ConfigurationBuilderExtensions
    {
        /// <summary>
        /// Read tenant settings from TenantDbContext
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantsGrpcConfiguration(this IServiceCollection services, string endpoint)
        {
            services.AddGrpcClient<TenantSettingsStore.TenantSettingsStoreClient>(o =>
            {
                o.Address = new Uri(endpoint);
            });
            return services.AddScoped<ITenantsConfigurationSource>(sp =>
            {
                var tenant = sp.GetService<ITenant>();
                var client = sp.GetRequiredService<TenantSettingsStore.TenantSettingsStoreClient>();
                var logger = sp.GetService<ILoggerFactory>();

                return new GrpcConfigurationSource(client, tenant, logger);
            });
        }

    }
}
