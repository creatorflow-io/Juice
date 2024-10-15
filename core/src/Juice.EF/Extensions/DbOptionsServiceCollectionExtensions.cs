using Juice.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DbOptionsServiceCollectionExtensions
    {
        public static IServiceCollection AddDbOptions<TContext>(this IServiceCollection services, Action<DbOptions>? configureOptions)
            where TContext : DbContext
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<TContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                if (string.IsNullOrEmpty(options.ConnectionName))
                {
                    options.ConnectionName = "Default";
                }
                return options;
            });

            return services;
        }

        public static IServiceCollection AddDbOptions<TContext>(this IServiceCollection services,
            IConfiguration configuration, Action<DbOptions>? configureOptions = default)
            where TContext : DbContext
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<TContext> { DatabaseProvider = "SqlServer" };
                configuration.Bind(options);
                configureOptions?.Invoke(options);
                return options;
            });

            return services;
        }

    }
}
