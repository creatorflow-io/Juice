using Juice.BgService.Management.File;
using Juice.Extensions.Configuration;
using Juice.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.BgService.Management.Extensions
{
    public static class ServiceManagerSeviceCollectionExtensions
    {
        public static IServiceCollection AddBgService(this IServiceCollection services, IConfigurationSection configuration)
        {
            services.ConfigureMutable<ServiceManagerOptions>(configuration);

            services.AddSingleton<ServiceManager>();

            services.AddSingleton<IServiceFactory, ServiceFactory>();

            services.AddHostedService(sp => sp.GetRequiredService<ServiceManager>());

            return services;
        }

        public static IServiceCollection UseFileStore(this IServiceCollection services,
            IConfigurationSection configuration)
        {
            services.ConfigureMutable<FileStoreOptions>(configuration,
                options =>
                {
                    var cfg = configuration.GetScalaredConfig<FileStoreOptions>();
                    if (cfg != null)
                    {
                        options.Services = cfg.Services;
                    }
                });
            services.AddSingleton<IServiceStore, FileStore>();

            return services;
        }
    }
}
