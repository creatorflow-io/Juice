using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Modular
{
    public static class DiscoveredModulesAppBuilderExtensions
    {
        /// <summary>
        /// Use this method to quick register MVC services with default options then register discovered modules.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IMvcBuilder AddDiscoveredModules(this WebApplicationBuilder app)
        {
            return app.Services.AddDiscoveredModules(app.Environment, app.Configuration);
        }

        /// <summary>
        /// Call Configure on all enabled modules
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        /// <exception cref="Exception"></exception>
        public static void ConfigureDiscoverdModules(this WebApplication app, IWebHostEnvironment env)
        {
            var startups = app.Services.GetServices<IModuleStartup>()
                .OrderBy(s => s.ConfigureOrder)
                .ToArray();

            var loggerFactory = app.Services.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("Startup");
            var hasError = false;

            foreach (var startup in startups)
            {
                try
                {
                    startup.Configure(app, app, env);
                }
                catch (Exception ex)
                {
                    hasError = true;
                    logger?.LogError("Failed to configure {service}. Message: {message}", startup.GetType().FullName, ex.Message);
                    logger?.LogTrace(ex.StackTrace);
                }
            }

            if (hasError)
            {
                throw new Exception("Some module failed. Please enable logging for Startup at Trace level for more information.");
            }
        }
    }
}
