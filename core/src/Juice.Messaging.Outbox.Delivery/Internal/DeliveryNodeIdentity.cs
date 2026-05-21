using Juice.Messaging.Outbox;

namespace Juice.Messaging.Outbox.Delivery.Internal
{
    internal sealed class DeliveryNodeIdentity : IDeliveryNodeIdentity
    {
        public string NodeId { get; } = $"{Environment.MachineName}:{Environment.ProcessId}";
    }
}
