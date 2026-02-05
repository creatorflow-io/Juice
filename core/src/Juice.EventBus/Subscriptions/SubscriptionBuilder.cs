namespace Juice.EventBus.Subscriptions
{
    public sealed class SubscriptionBuilder
    {
        private readonly List<SubscriptionInfo> _subscriptions = new();
        public bool HasSubscriptions => _subscriptions.Count > 0;
        public IReadOnlyList<SubscriptionInfo> Subscriptions => _subscriptions;
        public void Subscribe(Type eventType, Type handlerType, string? key = null)
        {
            _subscriptions.Add(SubscriptionInfo.Typed(eventType, handlerType, key));
        }

        public void Subscribe<TEvent, THandler>(string? key = null)
            where TEvent : IIntegrationEvent
            where THandler : class, IIntegrationEventHandler<TEvent>
        {
            _subscriptions.Add(SubscriptionInfo.Typed(typeof(TEvent), typeof(THandler), key));
        }

        public ISubscriptionsProvider Build() => new InMemorySubscriptionsProvider(_subscriptions);
    }
}
