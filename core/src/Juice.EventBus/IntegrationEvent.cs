using Newtonsoft.Json;

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

        public Guid Id { get; init; } = Guid.NewGuid();

        public DateTime CreationDate { get; init; } = DateTime.UtcNow;

        public virtual string GetEventKey()
        {
            return GetType().Name;
        }
    }
}
