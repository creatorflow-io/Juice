namespace Juice.Messaging.Outbox.Delivery
{
    public sealed record DeliveryContext(string PublisherKey, string Intent, string Context);
}
