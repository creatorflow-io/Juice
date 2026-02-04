using Juice.EventBus.Delivery;
using Juice.EventBus.Transactional.EF;
using Juice.EventBus.Transactional.EF.Intents;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OutboxDeliveryBuilderExtensions
    {
        public static EventBusBuilder AddOutbox(this EventBusBuilder builder)
        {
            builder.AddOutboxCore();
            builder.Services.TryAddScoped(typeof(IOutboxRepository<>), typeof(OutboxRepository<>));
            return builder;
        }

        public static EventBusBuilder AddDelivery(this EventBusBuilder builder, Action<DeliveryBuilder>? configure = default)
        {
            builder.AddDeliveryCore(delivery =>
            {
                configure?.Invoke(delivery);
                delivery.AddDefaultIntents();
            });
            
            return builder;
        }

        public static DeliveryBuilder AddDefaultIntents(this DeliveryBuilder builder)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.KeyedScoped(typeof(IOutboxIntent<>), "send-pending", typeof(SendPendingIntent<>)));
            builder.Services.TryAddEnumerable(ServiceDescriptor.KeyedScoped(typeof(IOutboxIntent<>), "retry-failed", typeof(RetryFailedIntent<>)));
            builder.Services.TryAddEnumerable(ServiceDescriptor.KeyedScoped(typeof(IOutboxIntent<>), "recover-timeout", typeof(RecoverTimeoutIntent<>)));
            return builder;
        }

        public static DeliveryBuilder Intent<TContext, TOutboxIntent>(this DeliveryBuilder builder)
            where TContext : class
            where TOutboxIntent : class, IOutboxIntent<TContext>
        {
            builder.Services.TryAddScoped<IOutboxIntent<TContext>, TOutboxIntent>();
            return builder;
        }

    }
}
