using Juice.Messaging;
using Juice.Messaging.Outbox.Delivery;
using Microsoft.Extensions.Configuration;
namespace Microsoft.Extensions.DependencyInjection
{
    public static class DeliveryOutboxBuilderExtensions
    {
        public static MessagingBuilder AddDelivery(this MessagingBuilder outboxBuilder, Action<DeliveryBuilder>? configure = default)
        {
            var builder = new DeliveryBuilder(outboxBuilder.Services);
            configure?.Invoke(builder);

            builder.RegisterEventTypeRegistry();
            return outboxBuilder;
        }

        public static MessagingBuilder AddDelivery(this MessagingBuilder outboxBuilder,
            IConfigurationSection deliveryPolicies)
        {
            outboxBuilder.AddDelivery(builder =>
            {
                builder.AddDeliveryPolicies(deliveryPolicies);
            });
            return outboxBuilder;
        }
    }
}
