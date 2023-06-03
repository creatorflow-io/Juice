using Microsoft.Extensions.DependencyInjection;

namespace Juice.MediatR.RequestManager.Redis
{
    public static class RedisRequestManagerServiceCollectionExtensions
    {
        /// <summary>
        /// Add Redis RequestManager to deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static IServiceCollection AddRedisRequestManager(this IServiceCollection services,
            Action<RedisOptions> configure)
        {
            services.Configure<RedisOptions>(configure);

            services.AddScoped<IRequestManager, RequestManager>();
            return services;
        }

        /// <summary>
        /// Add Redis RequestManager to deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static IServiceCollection AddRedisRequestManager(this IServiceCollection services)
            => services.AddRedisRequestManager(_ => { });

    }
}
