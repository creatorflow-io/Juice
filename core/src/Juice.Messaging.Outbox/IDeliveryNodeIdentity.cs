namespace Juice.Messaging.Outbox
{
    public interface IDeliveryNodeIdentity
    {
        string NodeId { get; }
    }
}
