using Juice.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Options
{
    public static class ConfigureTenantMutableServiceCollectionExtensions
    {

        /// <summary>
        /// Configure mutable options then you can update options value by inject <see cref="IOptionsMutable{T}"/>
        /// <para>WARN: Don't assign value to options directly like:</para>
        /// <code>services.ConfigureMutable(configuration, (UploadOptions options) => {</code>
        /// <code>Don't: options = configuration.GetScalaredConfig{UploadOptions}();</code>
        /// <code>Do: options.Abc = something;</code>
        /// <code>});</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="section"></param>
        /// <param name="configureOptions"></param>
		public static void ConfigureTenantsOptionsMutable<T>(
            this IServiceCollection services,
            string sectionKey,
            Action<T>? configureOptions = default) where T : class, new()
        {
            services.AddScoped<ITenantsOptionsMutable<T>>(provider =>
            {
                return new TenantsOptionsMutable<T>(provider,
                    sectionKey, configureOptions);
            });
        }

        public static void ConfigureTenantsOptions<T>(this IServiceCollection services, string sectionKey,
            Action<T>? configureOptions = default
            )
             where T : class, new()
        {
            services.AddScoped<ITenantsOptions<T>>(provider =>
            {
                return new TenantsOptions<T>(provider.GetRequiredService<ITenantsConfiguration>(),
                    sectionKey, configureOptions);
            });
        }
    }
}
