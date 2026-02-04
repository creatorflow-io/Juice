namespace Juice.EventBus.Registry
{
    /// <summary>
    /// Represents a registry for event types used in the event bus to deserialize event.
    /// </summary>
    public interface IEventTypeRegistry
    {
        void Register(Type type);
        void Merge(Type[] types);
        Type? Resolve(string eventTypeName);
    }

    public static class EventTypeRegistryExtensions
    {
        public static void Register<TEvent>(this IEventTypeRegistry registry)
            where TEvent : class, IIntegrationEvent
        {
            registry.Register(typeof(TEvent));
        }
    }
}
