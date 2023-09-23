using Juice.Plugins;
using Juice.Plugins.Internal;
using Juice.Plugins.Management;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class PluginServiceCollectionExtensions
    {
        public static IServiceCollection AddPlugins(this IServiceCollection services, Action<PluginOptions> configure)
        {
            var options = new PluginOptions();
            configure(options);
            services.AddSingleton<IPluginsManager>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<PluginsManager>>();
                var pluginsManager = new PluginsManager(options.AbsolutePaths, logger,
                    s => options.ConfigureSharedServices?.Invoke(s, sp)
                    );

                if (options.PluginLoaded != null)
                {
                    pluginsManager.PluginLoaded += options.PluginLoaded;
                }
                if (options.PluginUnloading != null)
                {
                    pluginsManager.PluginUnloading += options.PluginUnloading;
                }

                return pluginsManager;
            });

            services.AddScoped<IPluginServiceProvider, PluginServiceProvider>();

            return services;
        }
    }
}
