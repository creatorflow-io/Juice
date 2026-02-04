using Juice.EventBus.Delivery.Policies;

namespace Juice.EventBus.Delivery
{
    public interface IOutboxIntent
    {
        string Name { get; }
        Task<IEnumerable<OutboxDelivery>> RetrieveDeliveriesAsync(
            string publisherKey, DeliveryPolicy policy,
            CancellationToken cancellationToken);
    }
    public interface IOutboxIntent<TContext>: IOutboxIntent
    {

    }
}
