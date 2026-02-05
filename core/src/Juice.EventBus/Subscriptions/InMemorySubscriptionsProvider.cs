namespace Juice.EventBus.Subscriptions
{
    internal sealed class InMemorySubscriptionsProvider : ISubscriptionsProvider
    {
        private readonly IEnumerable<SubscriptionInfo> _subscriptions;
        public InMemorySubscriptionsProvider(IEnumerable<SubscriptionInfo> subscriptions)
        {
            _subscriptions = subscriptions;
        }
        public IEnumerable<SubscriptionInfo> GetSubscriptions()
            => _subscriptions;
    }
}
