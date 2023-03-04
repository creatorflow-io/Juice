using System.Text.Json.Serialization;

namespace Juice.EventBus
{
    public record IntegrationEvent
    {
        public IntegrationEvent()
        {

        }

        [JsonConstructor]
        public IntegrationEvent(Guid id, DateTime creationDate)
        {
            Id = id;
            CreationDate = creationDate;
        }

        [JsonInclude]
        public Guid Id { get; init; } = Guid.NewGuid();

        [JsonInclude]
        public DateTime CreationDate { get; init; } = DateTime.UtcNow;

        public virtual string GetEventKey()
        {
            return GetType().Name;
        }
    }
}
