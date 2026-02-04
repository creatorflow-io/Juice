namespace Juice.EventBus.Delivery
{
    public class OutboxEvent
    {
        public Guid EventId { get; init; }
        public string EventTypeName { get; init; }
        public DateTime CreationTime { get; init; }
        public string Payload { get; init; }
        public string? TransactionId { get; init; }
        public string? TenantId { get; init; } 

        public virtual ICollection<OutboxDelivery> Deliveries { get; init; } = [];
    }
}
