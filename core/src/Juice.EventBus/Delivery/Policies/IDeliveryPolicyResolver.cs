namespace Juice.EventBus.Delivery.Policies
{
    public interface IDeliveryPolicyResolver
    {
        ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken);
    }
}
