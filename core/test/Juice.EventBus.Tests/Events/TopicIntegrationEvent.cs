
using Juice.Messaging;

namespace Juice.EventBus.Tests.Events
{
    public record TopicIntegrationEvent(string Key) : IntegrationEvent
    {
        public override string EventName => Key;
    }
}
