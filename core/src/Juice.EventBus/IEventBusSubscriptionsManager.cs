namespace Juice.EventBus
{
    public interface IEventBusSubscriptionsManager
    {
        bool IsEmpty { get; }

        event EventHandler<string> OnEventRemoved;

        ValueTask AddSubscriptionAsync<T, TH>(string? key)
           where T : IntegrationEvent
           where TH : IIntegrationEventHandler<T>;

        ValueTask RemoveSubscriptionAsync<T, TH>(string? key)
             where TH : IIntegrationEventHandler<T>
             where T : IntegrationEvent;

        ValueTask<bool> HasSubscriptionsForEventAsync(string eventName);
        /// <summary>
        /// Return the registered event type by name
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        ValueTask<Type?> GetEventTypeByNameAsync(string eventName);
        void Clear();
        ValueTask<IEnumerable<SubscriptionInfo>> GetHandlersForEventAsync(string eventName);
        string GetDefaultEventKey(Type type);
    }

    public static class EventBusSubscriptionsManagerExtensions
    {
        public static string GetDefaultEventKey<T>(this IEventBusSubscriptionsManager subscriptionsManager)
            => subscriptionsManager.GetDefaultEventKey(typeof(T));

    }
}
