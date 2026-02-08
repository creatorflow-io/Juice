
using Juice.Messaging;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Delivery;
using Juice.Messaging.Outbox.EF;
using Juice.Messaging.Outbox.EF.Intents;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OutboxMessagingBuilderExtensions
    {
        /// <summary>
        /// Adds the Outbox services to the IServiceCollection to support the Outbox pattern.
        /// </summary>
        /// <param name="builder">The IServiceCollection instance.</param>
        /// <param name="configure"></param>
        /// <returns>The updated IServiceCollection instance.</returns>
        public static MessagingBuilder AddOutbox(this MessagingBuilder builder, Action<OutboxBuilder>? configure = default)
        {
            var outboxBuilder = new OutboxBuilder(builder.Services);
            configure?.Invoke(outboxBuilder);

            outboxBuilder.AddDefaultServices()
                .AddOutboxRepository()
                .AddDeliveryIntents();

            builder.AddOutboxCore();

            return builder;
        }
    }

    public sealed class OutboxBuilder
    {
        private readonly IServiceCollection _services;
        public IServiceCollection Services => _services;
        internal OutboxBuilder(IServiceCollection services)
        {
            _services = services;
        }

        internal OutboxBuilder AddDefaultServices()
        {
            // Register IntegrationEventService
            return this;
        }

        public OutboxBuilder AddOutboxRepository()
        {
            Services.TryAddScoped(typeof(IOutboxRepository<>), typeof(OutboxRepository<>));
            return this;
        }

        public OutboxBuilder AddDeliveryIntents()
        {
            Services.TryAddEnumerable(ServiceDescriptor.KeyedScoped(typeof(IDeliveryIntent<>), "send-pending", typeof(SendPendingIntent<>)));
            Services.TryAddEnumerable(ServiceDescriptor.KeyedScoped(typeof(IDeliveryIntent<>), "retry-failed", typeof(RetryFailedIntent<>)));
            Services.TryAddEnumerable(ServiceDescriptor.KeyedScoped(typeof(IDeliveryIntent<>), "recover-timeout", typeof(RecoverTimeoutIntent<>)));
            return this;
        }

        public OutboxBuilder Intent<TContext, TOutboxIntent>()
            where TContext : class
            where TOutboxIntent : class, IDeliveryIntent<TContext>
        {
            Services.TryAddScoped<IDeliveryIntent<TContext>, TOutboxIntent>();
            return this;
        }
    }

}
