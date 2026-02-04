namespace Juice.EventBus.Delivery.Policies
{
    public sealed record DeliveryContext(string PublisherKey, string Intent, string Context);
}
