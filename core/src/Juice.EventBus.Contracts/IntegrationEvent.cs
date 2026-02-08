namespace Juice.EventBus
{
    public abstract record IntegrationEvent: Message, IIntegrationEvent
    {
        public virtual string EventName => GetType().Name;
    }
}
