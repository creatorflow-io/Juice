using Juice.Messaging;
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

            services.TryAddScoped<IPostCommitActions, PostCommitActions>();

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
            where TOutbox : IOutboxService
        {
            services.TryAddScoped(typeof(TOutbox), sp => OutboxProxy<TOutbox>.Create(sp.GetRequiredService<IOutboxService<TContext>>())!);
            return services;
        }
    }
}
