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
        public static IServiceCollection AddDefaultUploadManager<T>(this IServiceCollection services, Action<UploadOptions> configure)
           where T : class, IFile, new()
        {
            services.Configure(configure);
            services.AddScoped<IUploadManager, DefaultUploadManager<T>>();
            return services;
        }

        public static IServiceCollection AddDefaultUploadManager<T>(this IServiceCollection services, IConfiguration configuration)
            where T : class, IFile, new()
        {
            services.Configure<UploadOptions>(configuration);
            services.AddScoped<IUploadManager, DefaultUploadManager<T>>();
            return services;
        }

        public static IServiceCollection AddInMemoryUploadManager(this IServiceCollection services, IConfiguration configuration, Action<UploadOptions>? configure = default)
        {
            services.Configure<InMemoryStorageOptions>(configuration);
            services.AddScoped<IStorageRepository, InMemoryStorageRepository>();
            if (configure != null)
            {
                services.AddDefaultUploadManager<UploadFileInfo>(configure);
            }
            else
            {
                services.AddDefaultUploadManager<UploadFileInfo>(configuration);
            }
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
