namespace Juice.Messaging.Outbox.Delivery
{
    public interface IDeliveryPolicyResolver
    {
        ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken);
    }
}
