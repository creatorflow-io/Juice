using Juice.Storage;
using Juice.Storage.Abstractions;
using Juice.Storage.InMemory;
using Juice.Storage.Services;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class InMemoryStorageServiceCollectionExtensions
    {
        public static IServiceCollection AddInMemoryUploadManager(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<InMemoryStorageOptions>(configuration);

            services.AddScoped<IStorageFactory, InMemoryStorageFactory>();
            services.AddScoped<IUploadManager, InMemoryUploadManager>();

            services.AddScoped<RequestEndpointAccessor>();
            services.AddScoped<IStorage, StorageProxy>();

            return services;
        }
    }
}
