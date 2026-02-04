namespace Juice.EventBus
{
    public abstract record IntegrationEvent: IIntegrationEvent
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public DateTime CreationDate { get; init; } = DateTime.UtcNow;

        public string? TenantId { get; init; }

        public string? Domain { get; init; }

        public virtual string GetEventKey()
        {
            return GetType().Name;
        }
    }
}
