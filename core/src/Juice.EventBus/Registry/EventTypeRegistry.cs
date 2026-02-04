using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Registry
{
    internal class EventTypeRegistry(IEnumerable<Type> types, ILogger logger) : IEventTypeRegistry
    {
        private readonly Dictionary<string, Type> _types = types.ToDictionary(t => t.Name, t => t);

        public Type? Resolve(string eventTypeName)
        {
            if (!_types.TryGetValue(eventTypeName, out var type))
            {
                logger.LogInformation(
                     "Unknown integration event type '{eventTypeName}'", eventTypeName);
                return null;
            }

            return type;
        }

        public void Merge(Type[] types)
        {
            foreach (var type in types)
            {
                Register(type);
            }
        }

        public void Register(Type eventType)
        {
            if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
                throw new ArgumentException(
                    $"Type '{eventType.FullName}' is not an integration event type.");
            _types[eventType.Name] = eventType;
        }
    }
}
