using Juice.Extensions.Options;
using Juice.Extensions.Options.Internal;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OptionsProviderServiceCollectionExtensions
    {
        public static IServiceCollection Configure<TService, TOptions>(
            this IServiceCollection services, Action<TOptions> configure)
        where TOptions : class, new()
        {
            services.AddSingleton<IOptionsProvider<TService, TOptions>>(sp =>
            {
                var options = new TOptions();
                configure(options);
                return new OptionsProvider<TService, TOptions>(options);
            });

            return services;
        }
    }
}
