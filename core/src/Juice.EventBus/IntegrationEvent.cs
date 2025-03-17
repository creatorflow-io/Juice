using Newtonsoft.Json;

namespace Juice.EventBus
{
    public record IntegrationEvent
    {
        public IntegrationEvent()
        {

        }

        [JsonConstructor]
        public IntegrationEvent(Guid id, DateTime creationDate, string? tenantId)
        {
            Id = id;
            CreationDate = creationDate;
            TenantId = tenantId;
        }

        public Guid Id { get; init; } = Guid.NewGuid();

        public DateTime CreationDate { get; init; } = DateTime.UtcNow;
        public string? TenantId { get; init; }

        public virtual string GetEventKey()
        {
            return GetType().Name;
        }
    }
}
