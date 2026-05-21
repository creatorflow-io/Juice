using Juice.Messaging.Outbox;

namespace Juice.Messaging.Outbox.Delivery.Internal
{
    internal sealed class DeliveryNodeIdentity : IDeliveryNodeIdentity
    {
        public string NodeId { get; }

        public DeliveryNodeIdentity()
        {
            var nodeId = $"{Environment.MachineName}:{Environment.ProcessId}";
            if (nodeId.Length > DeliveryNodeIdentityValidator.MaxLength)
            {
                throw new InvalidOperationException(
                    $"Computed node identity '{nodeId}' exceeds the maximum allowed length of {DeliveryNodeIdentityValidator.MaxLength} characters. " +
                    $"Use DeliveryBuilder.UseNodeIdentity(string) to supply a shorter identifier.");
            }
            NodeId = nodeId;
        }
    }
}
