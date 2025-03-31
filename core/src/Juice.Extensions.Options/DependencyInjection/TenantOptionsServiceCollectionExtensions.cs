using Juice.Extensions.Configuration;
using Juice.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TenantOptionsServiceCollectionExtensions
    {
        /// <summary>
        /// Configure options per tenant
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="sectionKey"></param>
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

        /// <summary>
        /// Configure mutable options per tenant
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="sectionKey"></param>

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
