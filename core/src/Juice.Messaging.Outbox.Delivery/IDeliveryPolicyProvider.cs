namespace Juice.Messaging.Outbox.Delivery
{
    public interface IDeliveryPolicyProvider
    {
        ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken);
    }
}
