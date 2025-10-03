using Microsoft.Extensions.Logging;

namespace Juice.EventBus
{
    public abstract class EventBusBase : IEventBus
    {
        protected readonly IEventBusSubscriptionsManager SubsManager;
        protected readonly ILogger Logger;

        public EventBusBase(IEventBusSubscriptionsManager subscriptionsManager,
            ILogger logger)
        {
            SubsManager = subscriptionsManager;
            Logger = logger;
        }

        public abstract ValueTask PublishAsync(IntegrationEvent @event, string? tenantId);

        public virtual ValueTask SubscribeAsync<T, TH>(string? key = default)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = key ?? SubsManager.GetDefaultEventKey<T>();
            Logger.LogInformation("Subscribing event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            SubsManager.AddSubscriptionAsync<T, TH>(key);
            return ValueTask.CompletedTask;
        }

        public virtual ValueTask UnsubscribeAsync<T, TH>(string? key = default)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = SubsManager.GetDefaultEventKey<T>();

            Logger.LogInformation("Unsubscribing event {EventName} for hanler {Handler}", eventName, typeof(TH).GetGenericTypeName());

            SubsManager.RemoveSubscriptionAsync<T, TH>(key);

            return ValueTask.CompletedTask;
        }

        public virtual ValueTask CloseAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
