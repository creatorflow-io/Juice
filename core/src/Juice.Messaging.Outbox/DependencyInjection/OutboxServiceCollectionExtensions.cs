
using Juice.Messaging;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OutboxServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Outbox services to the IServiceCollection to support the Outbox pattern.
        /// </summary>
        /// <param name="builder">The IServiceCollection instance.</param>
        /// <returns>The updated IServiceCollection instance.</returns>
        public static MessagingBuilder AddOutboxCore(this MessagingBuilder builder)
        {
            builder.Services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxService<>), typeof(OutboxEventService<>)));

            return builder;
        }

        /// <summary>
        /// Register outbox proxy for specific type.
        /// </summary>
        /// <typeparam name="TOutbox"></typeparam>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddOutboxProxy<TOutbox, TContext>(this IServiceCollection services)
            where TOutbox: IOutboxService
        {
            services.TryAddScoped(typeof(TOutbox), sp => OutboxProxy<TOutbox>.Create(sp.GetRequiredService<IOutboxService<TContext>>())!);
            return services;
        }
    }
}
