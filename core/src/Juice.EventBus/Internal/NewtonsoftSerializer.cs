using Newtonsoft.Json;

namespace Juice.EventBus.Internal
{
    internal class NewtonsoftSerializer : IEventSerializer
    {
        public IIntegrationEvent? Deserialize(string payload, Type eventType)
            => JsonConvert.DeserializeObject(payload, eventType) as IIntegrationEvent;
        public string Serialize(IIntegrationEvent @event)
            => JsonConvert.SerializeObject(@event);
    }
}
