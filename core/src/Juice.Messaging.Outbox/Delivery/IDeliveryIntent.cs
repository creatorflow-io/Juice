namespace Juice.Messaging.Outbox.Delivery
{
    public interface IDeliveryIntent
    {
        string Name { get; }
        Task<IEnumerable<OutboxDelivery>> RetrieveDeliveriesAsync(
            string publisherKey, DeliveryPolicy policy,
            CancellationToken cancellationToken);
    }
    public interface IDeliveryIntent<TContext>: IDeliveryIntent
    {

    }
}
