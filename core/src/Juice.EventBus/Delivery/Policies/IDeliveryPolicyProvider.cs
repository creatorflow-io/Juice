namespace Juice.EventBus.Delivery.Policies
{
    public interface IDeliveryPolicyProvider
    {
        ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken);
    }
}
