using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Extensions.Options
{
    public static class OptionsStoreServiceCollectionExtensions
    {
        public static IServiceCollection UseDefaultOptionsMutableStore(this IServiceCollection services, string? file = default)
        {
            services.TryAddTransient<IOptionsMutableStore>(sp =>
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                return new DefaultOptionsMutableStore(env, file);
            });
            return services;
        }

        public static IServiceCollection UseDefaultOptionsMutableStore<T>(this IServiceCollection services, string? file = default)
        {
            services.TryAddTransient<IOptionsMutableStore<T>>(sp =>
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                return new DefaultOptionsMutableStore<T>(env, file);
            });
            return services;
        }
    }
}
