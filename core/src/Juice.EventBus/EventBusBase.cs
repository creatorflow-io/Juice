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

        public abstract Task PublishAsync(IntegrationEvent @event);

        public virtual void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = SubsManager.GetEventKey<T>();
            Logger.LogInformation("Subscribing event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            SubsManager.AddSubscription<T, TH>();

        }

        public virtual void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = SubsManager.GetEventKey<T>();

            Logger.LogInformation("Unsubscribing event {EventName} for hanler {Handler}", eventName, typeof(TH).GetGenericTypeName());

            SubsManager.RemoveSubscription<T, TH>();

        }

    }
}
