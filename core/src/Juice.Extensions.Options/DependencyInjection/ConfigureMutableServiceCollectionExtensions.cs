using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Options.DependencyInjection
{
    public static class ConfigureMutableServiceCollectionExtensions
    {
        /// <summary>
        /// Configure mutable options then you can update options value by inject <see cref="IOptionsMutable{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="section"></param>
		public static void ConfigureMutable<T>(
            this IServiceCollection services,
            IConfigurationSection section) where T : class, new()
        {
            services.Configure<T>(section);
            services.AddTransient<IOptionsMutable<T>>(provider =>
            {
                return new OptionsMutable<T>(provider, section.Path);
            });
        }

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
		public static void ConfigureMutable<T>(
            this IServiceCollection services,
            IConfigurationSection section,
            Action<T> configureOptions) where T : class, new()
        {
            services.Configure<T>(section);
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            services.AddTransient<IOptionsMutable<T>>(provider =>
            {
                return new OptionsMutable<T>(provider, section.Path, configureOptions);
            });
        }

    }
}
