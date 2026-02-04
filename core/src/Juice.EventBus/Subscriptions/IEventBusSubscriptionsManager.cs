namespace Juice.EventBus.Subscriptions
{
    public interface IEventBusSubscriptionsManager
    {
        bool IsEmpty { get; }

        event EventHandler<string> OnEventRemoved;

        void AddSubscription(Type eventType, Type handlerType, string? key = default);

        ValueTask<bool> HasSubscriptionsForEventAsync(string eventName);
        /// <summary>
        /// Return the registered event type by name
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        ValueTask<Type?> GetEventTypeByNameAsync(string eventName);
        ValueTask<IEnumerable<SubscriptionInfo>> GetHandlersForEventAsync(string eventName);
        string GetDefaultEventKey(Type type);
    }

    public static class EventBusSubscriptionsManagerExtensions
    {
        public static string GetDefaultEventKey<T>(this IEventBusSubscriptionsManager subscriptionsManager)
            => subscriptionsManager.GetDefaultEventKey(typeof(T));
    }
}
