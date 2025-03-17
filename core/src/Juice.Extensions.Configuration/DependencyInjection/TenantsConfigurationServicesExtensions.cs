using Juice.Extensions.Configuration;
using Juice.MultiTenant;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TenantsConfigurationServicesExtensions
    {
        public static IServiceCollection AddTenantsConfiguration(
            this IServiceCollection services)
        {
            return services.AddScoped<ITenantsConfiguration, TenantsConfiguration>();
        }

        public static IServiceCollection AddTenantsJsonFile(
            this IServiceCollection services, string path, bool optional = true, bool reloadOnChange = true)
        {
            return services.AddScoped<ITenantsConfigurationSource>(sp =>
            {
                var tenant = sp.GetService<ITenantAccessor>()?.Tenant;
                var source = new TenantsJsonConfigurationSource
                {
                    Tenant = tenant,
                    Path = path,
                    Optional = optional,
                    ReloadOnChange = reloadOnChange
                };
                source.ResolveFileProvider();
                return source;
            });
        }

    }
}
