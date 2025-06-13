namespace Juice.EventBus
{
    public abstract class EventBusWrapper : IEventBus
    {
        private readonly IEventBus _eventBus;
        public EventBusWrapper(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }
        public Task PublishAsync(IntegrationEvent @event, string? tenantId = null)
            => _eventBus.PublishAsync(@event, tenantId);

        public void Subscribe<T, TH>(string? key = null)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
            => _eventBus.Subscribe<T, TH>(key);

        public void Unsubscribe<T, TH>(string? key = null)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
            => _eventBus.Unsubscribe<T, TH>(key);
    }
}
