namespace Juice.EventBus.Subscriptions
{
    /// <summary>
    /// Marker interface for subscription providers that supply handler registrations
    /// for in-process (<c>"local"</c> / <c>"local-channel"</c>) dispatch routes.
    /// Keeping these registrations separate from <see cref="ISubscriptionsProvider"/>
    /// prevents them from being picked up by broker (e.g. RabbitMQ) consumer managers.
    /// </summary>
    public interface ILocalSubscriptionsProvider : ISubscriptionsProvider
    {
    }
}
