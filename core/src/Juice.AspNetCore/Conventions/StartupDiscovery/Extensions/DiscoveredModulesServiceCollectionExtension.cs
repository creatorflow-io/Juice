using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;

namespace Juice.Conventions.StartupDiscovery.Extensions
{
    public static class DiscoveredModulesServiceCollectionExtension
    {
        /// <summary>
		/// Use this method to quick register MVC services with default options then register discovered modules.
		/// </summary>
		/// <param name="services"></param>
        /// <param name="env"></param>
		/// <param name="configuration"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public static IMvcBuilder AddDiscoveredModules(this IServiceCollection services, IWebHostEnvironment env, IConfigurationRoot configuration)
        {
            services.AddHttpContextAccessor();

            var mvc = services.AddControllers(options =>
            {
            })
            .AddDataAnnotationsLocalization()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ContractResolver = new DefaultContractResolver();
            })
            .AddDiscoveredModules(env, configuration)
            ;

            return mvc;
        }
    }
}
