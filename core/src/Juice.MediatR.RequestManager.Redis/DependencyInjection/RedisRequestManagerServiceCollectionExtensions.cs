using Juice.MediatR;
using Juice.MediatR.RequestManager.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RedisRequestManagerServiceCollectionExtensions
    {
        /// <summary>
        /// Add Redis RequestManager to deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static IServiceCollection AddRedisMediatorRequestManager(this IServiceCollection services,
            Action<RedisOptions> configure)
        {
            services.Configure(configure);

            services.TryAddScoped<IRequestManager, RequestManager>();
            services.TryAddScoped(typeof(IRequestManager<>), typeof(RequestManager<>));
            return services;
        }
    }
}
