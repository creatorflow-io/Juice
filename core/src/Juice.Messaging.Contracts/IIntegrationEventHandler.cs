namespace Juice.Messaging
{
    public interface IIntegrationEventHandler<in TIntegrationEvent>
        where TIntegrationEvent : IIntegrationEvent
    {
        Task HandleAsync(TIntegrationEvent @event);
    }
}
