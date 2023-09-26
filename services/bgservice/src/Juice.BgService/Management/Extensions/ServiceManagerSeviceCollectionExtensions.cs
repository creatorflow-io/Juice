using Juice.BgService.Management.File;
using Juice.Extensions.Configuration;
using Juice.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.BgService.Management
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
            services.AddSingleton<IServiceRepository, FileStore>();

            return services;
        }
    }
}
