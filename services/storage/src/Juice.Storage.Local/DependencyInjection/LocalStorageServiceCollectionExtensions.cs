using Juice.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Storage.Local
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
