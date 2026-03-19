
using Juice.Messaging;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Delivery;
using Juice.Messaging.Outbox.EF;
using Juice.Messaging.Outbox.EF.Intents;
using Microsoft.EntityFrameworkCore;
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

        /// <summary>
        /// Registers <see cref="DefaultOutboxContext"/> and backs <see cref="IMessageService"/>
        /// with <c>MessageService&lt;DefaultOutboxContext&gt;</c>, enabling full-route publishing
        /// (<c>"local-channel"</c>, <c>"local"</c>, and broker routes) outside domain transactions.
        /// <para>
        /// Tables must exist — run <c>OutboxContext</c> migrations against the target database.
        /// No separate migration project is required for <see cref="DefaultOutboxContext"/>.
        /// </para>
        /// <para>
        /// Delivery is NOT auto-configured by this overload; call <c>AddDelivery()</c> separately
        /// or use the <c>AddDefaultMessageService(configure, autoWireDelivery: true)</c> overload
        /// from <c>Juice.Messaging.Outbox.Delivery</c>.
        /// </para>
        /// <para>
        /// <b>Warning</b>: <c>IMessageService</c> is registered with <c>TryAddScoped</c> — first
        /// registration wins. Calling both <c>AddMessageService()</c> and
        /// <c>AddDefaultMessageService()</c> is a misconfiguration; only the first will take effect.
        /// </para>
        /// </summary>
        /// <param name="builder">The <see cref="MessagingBuilder"/> instance.</param>
        /// <param name="configure">EF Core options (connection string, provider).</param>
        public static MessagingBuilder AddDefaultMessageService(
            this MessagingBuilder builder,
            Action<DbContextOptionsBuilder> configure)
        {
            builder.Services.AddDbContext<DefaultOutboxContext>(configure);
            builder.AddOutbox();
            builder.AddMessageService<DefaultOutboxContext>();
            builder.Services.TryAddScoped<IMessageService>(sp =>
                sp.GetRequiredService<IMessageService<DefaultOutboxContext>>());
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
