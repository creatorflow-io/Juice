using Juice.Tenants;
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
                var tenant = sp.GetService<ITenant>();
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                return new DefaultOptionsMutableStore(tenant, env, file);
            });
            return services;
        }

        public static IServiceCollection UseDefaultOptionsMutableStore<T>(this IServiceCollection services, string? file = default)
        {
            services.TryAddTransient<IOptionsMutableStore<T>>(sp =>
            {
                var tenant = sp.GetService<ITenant>();
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                return new DefaultOptionsMutableStore<T>(tenant, env, file);
            });
            return services;
        }
    }
}
