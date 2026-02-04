using Juice.Extensions.Redis;
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
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static MediatorBuilder AddRedisRequestManager(this MediatorBuilder builder,
            Action<RedisOptions> configure)
        {
            builder.Services.TryAddRedisConnectionProvider<RequestManager>(configure);

            builder.Services.TryAddScoped<IRequestManager, RequestManager>();
            builder.Services.TryAddScoped(typeof(IRequestManager<>), typeof(RequestManager<>));
            return builder;
        }
    }
}
