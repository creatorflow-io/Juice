namespace Juice.EventBus.Subscriptions
{
    public interface ISubscriptionsManager
    {
        bool IsEmpty { get; }

        void AddSubscription(Type eventType, Type handlerType, string? key = default);

        ValueTask<bool> HasSubscriptionsForEventAsync(string eventName);
        /// <summary>
        /// Return the registered event type by name
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        ValueTask<Type?> GetEventTypeByNameAsync(string eventName);
        ValueTask<IEnumerable<Type>> GetHandlersForEventAsync(string eventName);
    }

}
