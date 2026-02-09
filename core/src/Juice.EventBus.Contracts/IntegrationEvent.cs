namespace Juice.EventBus
{
    public abstract record IntegrationEvent: MessageBase, IIntegrationEvent
    {
        public virtual string EventName => GetType().Name;
    }
}
