using Juice.Storage.Abstractions;
using Juice.Storage.Local;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class LocalStorageServiceCollectionExtensions
    {
        public static IServiceCollection AddLocalStorageProviders(this IServiceCollection services)
        {
            services.AddScoped<IStorageProvider, LocalStorageProvider>();
            services.AddScoped<IStorageProvider, FTPStorageProvider>();

            return services;
        }
    }
}
