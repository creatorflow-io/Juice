namespace Juice.EventBus.Subscriptions
{
    public class SubscriptionInfo
    {
        public bool IsDynamic { get; }
        public string Key { get; init; } = string.Empty;
        public Type EventType { get; }
        public Type HandlerType { get; }

        private SubscriptionInfo(bool isDynamic, Type eventType, Type handlerType, string? key)
        {
            IsDynamic = isDynamic;
            HandlerType = handlerType;
            EventType = eventType;
            Key = key ?? eventType.Name;
        }

        public static SubscriptionInfo Dynamic(Type eventType, Type handlerType, string? key = default)
        {
            return new SubscriptionInfo(true, eventType, handlerType, key);
        }
        public static SubscriptionInfo Typed(Type eventType, Type handlerType, string? key = default)
        {
            return new SubscriptionInfo(false, eventType, handlerType, key);
        }
    }
}
