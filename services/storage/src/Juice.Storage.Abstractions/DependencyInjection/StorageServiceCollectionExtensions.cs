using Juice.Storage.Abstractions;
using Juice.Storage.Abstractions.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class StorageServiceCollectionExtensions
    {
        public static IServiceCollection AddStorage(this IServiceCollection services)
        {
            services.AddScoped<IStorageProviderFactory, DefaultStorageProviderFactory>();
            services.AddScoped<IStorageResolveStrategy, RepoStorageResolver>();
            services.AddScoped<IStorageResolver, StorageResolver>();
            services.AddScoped<IStorage>(sp =>
                sp.GetRequiredService<IStorageResolver>().Storage
                ?? throw new InvalidOperationException("Storage is not resolved, try to call IStorageResolver.TryResolveAsync first.")
            );
            return services;
        }

        /// <summary>
        /// Only for testing purposes, it will increase memory usage
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        [Obsolete("Only for testing purposes, it will increase memory usage", error: false)]
        public static IServiceCollection AddImMemoryStorageProvider(this IServiceCollection services)
        {
            services.AddSingleton<InMemoryStorageProvider>();
            services.AddTransient<IStorageProvider>(sp => sp.GetRequiredService<InMemoryStorageProvider>());
            return services;
        }

    }
}
