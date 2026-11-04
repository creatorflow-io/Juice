using Juice.Messaging;
using Juice.Messaging.Integrations;
using Juice.Messaging.Internal;
using Juice.Messaging.Outbox;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MessagingServiceCollectionExtensions
    {
        public static MessagingBuilder AddMessaging(this IServiceCollection services, Action<MessagingBuilder>? buildAction = default)
        {
            var builder = new MessagingBuilder(services);
            buildAction?.Invoke(builder);

            builder.AddDefaultSerializer();

            builder.AddIntegrationEventDispatcher();

            return builder;
        }

        /// <summary>
        /// Adds an <see cref="IIntegrationEventPipelineBehavior"/> that runs around the handlers of every integration event,
        /// in their DI scope. Behaviors run by <see cref="IIntegrationEventPipelineBehavior.Order"/>, the lowest outermost;
        /// adding the same behavior twice has no effect.
        /// </summary>
        public static IServiceCollection AddIntegrationEventPipelineBehavior<TBehavior>(this IServiceCollection services)
            where TBehavior : class, IIntegrationEventPipelineBehavior
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IIntegrationEventPipelineBehavior, TBehavior>());
            return services;
        }

        /// <summary>
        /// Register outbox proxy for specific type.
        /// </summary>
        /// <typeparam name="TOutbox"></typeparam>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddOutboxProxy<TOutbox, TContext>(this IServiceCollection services)
            where TOutbox : IOutboxService
        {
            services.TryAddScoped(typeof(TOutbox), sp => OutboxProxy<TOutbox>.Create(sp.GetRequiredService<IOutboxService<TContext>>())!);
            return services;
        }
    }
}
