using Juice.Extensions.Options.Stores;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OptionsFileStoreServiceCollectionExtensions
    {
        /// <summary>
        /// Save options to [file.json]
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection UseOptionsMutableFileStore(this IServiceCollection services, string file)
        {
            services.TryAddSingleton<IOptionsMutableStore>(sp => new DefaultOptionsMutableStore(file));
            return services;
        }

        /// <summary>
        /// Save options to [custome file.json] for specified input type/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection UseOptionsMutableFileStore<T>(this IServiceCollection services, string file)
        {
            services.TryAddSingleton<IOptionsMutableStore<T>>(sp => new DefaultOptionsMutableStore<T>(file));
            return services;
        }
    }
}
