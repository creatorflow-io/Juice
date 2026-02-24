namespace Juice.EventBus
{
    [Obsolete("Use Juice.Messaging.IIntegrationEventHandler<T> from Juice.Messaging.Contracts instead.", false)]
    public interface IIntegrationEventHandler<in TIntegrationEvent> : Messaging.IIntegrationEventHandler<TIntegrationEvent>
        where TIntegrationEvent : IIntegrationEvent
    {
    }
}
