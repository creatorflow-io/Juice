namespace Juice.EventBus
{
    [Obsolete("Use Juice.Messaging.IntegrationEvent from Juice.Messaging.Contracts instead.", false)]
    public abstract record IntegrationEvent : Juice.Messaging.IntegrationEvent, IIntegrationEvent
    {
    }
}
