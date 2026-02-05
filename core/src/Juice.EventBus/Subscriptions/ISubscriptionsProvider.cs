
namespace Juice.EventBus.Subscriptions
{
    public interface ISubscriptionsProvider
    {
        IEnumerable<SubscriptionInfo> GetSubscriptions();
    }
}
