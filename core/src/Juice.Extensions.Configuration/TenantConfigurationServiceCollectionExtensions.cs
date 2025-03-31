using Juice.MultiTenant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Configuration
{
    public static class TenantConfigurationServiceCollectionExtensions
    {
        public static IServiceCollection AddTenantConfiguration(this IServiceCollection services)
        {
            return services.AddSingleton<ITenantConfiguration, TenantConfiguration>();
        }

        /// <summary>
        /// Add a configuration source that reads from a json file per tenant
        /// </summary>
        /// <param name="services"></param>
        /// <param name="path"></param>
        /// <param name="optional"></param>
        /// <param name="reloadOnChange"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantJsonFile(this IServiceCollection services, string path, bool optional = false, bool reloadOnChange = false)
        {
            return services.AddSingleton<IConfigurationSource>(sp =>
            {
                var tenantAccessor = sp.GetRequiredService<ITenantAccessor>();
                return new TenantFileConfigurationSource
                {
                    TenantAccessor = tenantAccessor,
                    Optional = optional,
                    ReloadOnChange = reloadOnChange,
                    Path = path,
                    FileProvider = null
                };
            });
        }

    }
}
