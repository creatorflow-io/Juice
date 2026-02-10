using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Idempotency.Cache;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IdempotencyInMemoryMessagingBuilderExtensions
    {
        /// <summary>
        /// Adds in-memory idempotency service to the messaging builder for testing purposes.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MessagingBuilder AddIdempotencyInMemory(this MessagingBuilder builder)
        {
            builder.Services.TryAddScoped<IIdempotencyService, InMemoryIdempotencyService>();
            return builder;
        }

        /// <summary>
        /// Adds distributed cache based idempotency service to the messaging builder for simple scenarios.
        /// Considers using RedisIdempotencyService for production scenarios.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MessagingBuilder AddIdempotencyDistributedCache(this MessagingBuilder builder)
        {
            builder.Services.TryAddScoped<IIdempotencyService, DistributecCacheIdempotencyService>();
            return builder;
        }
    }
}
