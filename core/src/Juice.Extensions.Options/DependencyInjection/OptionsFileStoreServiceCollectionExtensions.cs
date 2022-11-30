using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Extensions.Options
{
    public static class OptionsFileStoreServiceCollectionExtensions
    {
        public static IServiceCollection UseFileOptionsMutableStore(this IServiceCollection services, string file)
        {
            services.TryAddTransient<IOptionsMutableStore>(sp => new DefaultOptionsMutableStore(file));
            return services;
        }

        public static IServiceCollection UseFileOptionsMutableStore<T>(this IServiceCollection services, string file)
        {
            services.TryAddTransient<IOptionsMutableStore<T>>(sp => new DefaultOptionsMutableStore<T>(file));
            return services;
        }
    }
}
