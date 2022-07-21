using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus
{
    /// <summary>
    /// Local event bus throught memory
    /// </summary>
    public class InMemoryEventBus : EventBusBase
    {
        private readonly IServiceScopeFactory _scopeFactory;
        public InMemoryEventBus(IEventBusSubscriptionsManager subscriptionsManager,
            IServiceScopeFactory scopeFactory,
            ILogger<InMemoryEventBus> logger) : base(subscriptionsManager, logger)
        {
            _scopeFactory = scopeFactory;
        }

        public override Task PublishAsync(IntegrationEvent @event) => ProcessingEventAsync(SubsManager.GetEventKey(@event), @event);

        protected virtual async Task ProcessingEventAsync(string eventName, IntegrationEvent @event)
        {

            Logger.LogTrace("Processing integration event: {EventName}", eventName);

            if (SubsManager.HasSubscriptionsForEvent(eventName))
            {
                using var scope = _scopeFactory.CreateScope();
                var subscriptions = SubsManager.GetHandlersForEvent(eventName);
                foreach (var subscription in subscriptions)
                {

                    var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                    if (handler == null) { continue; }
                    var eventType = SubsManager.GetEventTypeByName(eventName);

                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

                    await (Task)concreteType.GetMethod("HandleAsync").Invoke(handler, new object[] { @event });
                }
            }
            else
            {
                Logger.LogWarning("No subscription for integration event: {EventName}", eventName);
            }
        }

    }
}
