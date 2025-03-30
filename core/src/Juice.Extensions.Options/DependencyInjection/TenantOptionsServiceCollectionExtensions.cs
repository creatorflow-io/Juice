using Juice.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Options
{
    public static class TenantOptionsServiceCollectionExtensions
    {
        public static void ConfigurePerTenant<T>(this IServiceCollection services, string sectionKey)
         where T : class, new()
        {
            services.AddSingleton<IConfigureOptions<T>>(sp =>
            {
                return new ConfigureOptions<T>(
                    (options) =>
                    {
                        sp.GetRequiredService<ITenantConfiguration>().GetSection(sectionKey).Bind(options);
                    });
            }
           );
        }

        public static void ConfigureMutablePerTenant<T>(this IServiceCollection services, string sectionKey)
            where T : class, new()
        {
            services.AddScoped<IOptionsMutable<T>>(provider =>
            {
                return new TenantOptionsMutable<T>(provider, sectionKey);
            });
            services.AddSingleton<IConfigureOptions<T>>(sp =>
            {
                return new ConfigureOptions<T>(
                    (options) =>
                    {
                        sp.GetRequiredService<ITenantConfiguration>().GetSection(sectionKey).Bind(options);
                    });
            }
           );
        }
    }
}
