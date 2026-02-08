using Newtonsoft.Json.Linq;

namespace Juice.Messaging.Outbox
{
    public class OutboxEvent
    {
        public Guid EventId { get; init; }
        public string EventTypeName { get; init; }
        public DateTimeOffset CreationTime { get; init; }
        public byte[] PayloadBytes { get; init; }
        public Dictionary<string, object?> Headers { get; init; } = new();
        public string? TransactionId { get; init; }
        public string? TenantId { get; init; }

        public virtual ICollection<OutboxDelivery> Deliveries { get; init; } = [];
    }
}
