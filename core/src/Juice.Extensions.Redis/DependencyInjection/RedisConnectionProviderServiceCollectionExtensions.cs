using Juice.Extensions.Redis;
using Juice.Extensions.Redis.Internal;
using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RedisConnectionProviderServiceCollectionExtensions
    {
        /// <summary>
        /// Try add Redis connection provider for specific type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection TryAddRedisConnectionProvider<T>(
            this IServiceCollection services, Action<RedisOptions> configure)
            where T : class
        {
            if(services.Any(sd => sd.ServiceType == typeof(IRedisConnectionProvider<T>)))
            {
                return services;
            }
            services.Configure<IRedisConnectionProvider<T>, RedisOptions>(configure);
            services.AddSingleton<IRedisConnectionProvider<T>, RedisConnectionProvider<T>>();
            return services;
        }

        /// <summary>
        /// Try add Redis connection provider
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection TryAddRedisConnectionProvider(
            this IServiceCollection services, Action<RedisOptions> configure)
        {
            if (services.Any(sd => sd.ServiceType == typeof(IRedisConnectionProvider)))
            {
                return services;
            }
            services.Configure<IRedisConnectionProvider, RedisOptions>(configure);
            services.AddSingleton<IRedisConnectionProvider, RedisConnectionProvider>();
            return services;
        }
    }
}
