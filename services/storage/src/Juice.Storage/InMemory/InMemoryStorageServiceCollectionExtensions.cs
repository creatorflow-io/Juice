using Juice.Storage;
using Juice.Storage.Abstractions;
using Juice.Storage.InMemory;
using Microsoft.Extensions.Configuration;

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
    }
}
