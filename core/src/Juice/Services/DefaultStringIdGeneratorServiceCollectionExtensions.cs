using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Services
{
    public static class DefaultStringIdGeneratorServiceCollectionExtensions
    {
        public static IServiceCollection AddDefaultStringIdGenerator(this IServiceCollection services)
        {
            services.TryAddScoped<IStringIdGenerator, DefaultStringIdGenerator>();
            return services;
        }
    }
}
