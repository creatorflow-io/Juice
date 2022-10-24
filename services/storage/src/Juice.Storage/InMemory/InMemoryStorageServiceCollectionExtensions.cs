using Juice.Storage;
using Juice.Storage.InMemory;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class InMemoryStorageServiceCollectionExtensions
    {
        public static IServiceCollection AddInMemoryUploadManager(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<InMemoryStorageOptions>(configuration);

            services.AddScoped<InMemoryStorageFactory>();
            services.AddScoped<IUploadManager, InMemoryUploadManager>();

            return services;
        }
    }
}
