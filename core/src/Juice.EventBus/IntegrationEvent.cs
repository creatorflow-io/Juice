namespace Juice.EventBus
{
    public abstract record IntegrationEvent
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public DateTime CreationDate { get; init; } = DateTime.UtcNow;

        public virtual string GetEventKey()
        {
            return GetType().Name;
        }
    }
}
