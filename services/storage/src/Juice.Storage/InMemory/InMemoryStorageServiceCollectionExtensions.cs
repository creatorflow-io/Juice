using Juice.Storage;
using Juice.Storage.Abstractions;
using Juice.Storage.BackgroundTasks;
using Juice.Storage.InMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class InMemoryStorageServiceCollectionExtensions
    {
        public static IServiceCollection AddInMemoryUploadManager(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<InMemoryStorageOptions>(configuration);
            services.Configure<UploadOptions>(configuration);
            services.AddScoped<IStorageRepository, InMemoryStorageRepository>();
            services.AddScoped<IUploadManager, DefaultUploadManager<UploadFileInfo>>();
            services.AddSingleton<IUploadRepository<UploadFileInfo>, InMemoryUploadRepository>();

            return services;
        }

        public static IServiceCollection AddInMemoryStorageMaintainServices(this IServiceCollection services,
            IConfiguration configuration, string[] identities,
            Action<StorageMaintainOptions>? configure = default)
        {
            services.Configure<StorageMaintainOptions>(configuration);
            if (configure != null)
            {
                services.Configure(configure);
            }
            foreach (var identity in identities)
            {
                services.AddTransient<IHostedService>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                    return new CleanupTimedoutUploadService<UploadFileInfo>(loggerFactory, scopeFactory, identity);
                });
            }

            return services;
        }
    }
}
