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

        /// <summary>
        /// Return the handlers for an event instance: those subscribed without a route to <paramref name="eventType"/>
        /// or one of its base types, plus those whose route (exact or pattern) matches <paramref name="eventName"/> and
        /// whose event type accepts <paramref name="eventType"/>. Each handler is returned once. The default
        /// implementation falls back to <see cref="GetHandlersForEventAsync(string)"/>, which matches routes only.
        /// </summary>
        /// <param name="eventName">The event's <c>EventName</c> (its routing key).</param>
        /// <param name="eventType">The event's runtime type.</param>
        ValueTask<IEnumerable<Type>> GetHandlersForEventAsync(string eventName, Type eventType)
            => GetHandlersForEventAsync(eventName);
    }

}
