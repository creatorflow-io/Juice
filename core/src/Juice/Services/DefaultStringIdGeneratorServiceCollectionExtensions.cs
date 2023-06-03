using Microsoft.Extensions.DependencyInjection;

namespace Juice.Services
{
    public static class DefaultStringIdGeneratorServiceCollectionExtensions
    {
        public static IServiceCollection AddDefaultStringIdGenerator(this IServiceCollection services)
        {
            services.AddScoped<IStringIdGenerator, DefaultStringIdGenerator>();
            return services;
        }
    }
}
