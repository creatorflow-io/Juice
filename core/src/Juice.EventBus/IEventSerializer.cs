namespace Juice.EventBus
{
    public interface IEventSerializer
    {
        IIntegrationEvent? Deserialize(string payload, Type eventType);
        string Serialize(IIntegrationEvent @event);
    }
}
