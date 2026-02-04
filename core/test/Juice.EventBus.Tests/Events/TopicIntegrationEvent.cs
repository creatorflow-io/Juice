
namespace Juice.EventBus.Tests.Events
{
    public record TopicIntegrationEvent(string Key) : IntegrationEvent
    {
        public override string GetEventKey() => Key;
    }
}
