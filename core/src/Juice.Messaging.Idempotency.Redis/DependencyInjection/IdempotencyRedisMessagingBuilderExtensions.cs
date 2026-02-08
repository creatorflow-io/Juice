using Juice.Extensions.Redis;
using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Idempotency.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IdempotencyRedisMessagingBuilderExtensions
    {
        /// <summary>
        /// Add Redis RedisIdempotencyService to deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static MessagingBuilder AddIdempotencyRedis(this MessagingBuilder builder,
            Action<RedisOptions> configure)
        {
            builder.Services.TryAddRedisConnectionProvider<RedisIdempotencyService>(configure);

            builder.Services.TryAddScoped<IIdempotencyService, RedisIdempotencyService>();
            return builder;
        }
    }
}
