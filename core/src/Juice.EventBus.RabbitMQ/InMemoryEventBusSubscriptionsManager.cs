using Microsoft.Extensions.Logging;

namespace Juice.EventBus.RabbitMQ
{
    public class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
    {

        private readonly Dictionary<string, List<SubscriptionInfo>> _handlers;
        private readonly Dictionary<string, Type> _eventTypes;

        private readonly ILogger _logger;

        public event EventHandler<string> OnEventRemoved;

        private Guid _guid = Guid.NewGuid();

        public InMemoryEventBusSubscriptionsManager(ILogger<InMemoryEventBusSubscriptionsManager> logger)
        {
            _handlers = new Dictionary<string, List<SubscriptionInfo>>();
            _eventTypes = new Dictionary<string, Type>();
            _logger = logger;
        }

        public bool IsEmpty => !_handlers.Keys.Any();
        public void Clear() => _handlers.Clear();

        public void AddSubscription<T, TH>(string? key)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = key ?? this.GetEventKey<T>();

            DoAddSubscription(typeof(TH), eventName, isDynamic: false);

            _eventTypes[eventName] = typeof(T);
        }

        private void DoAddSubscription(Type handlerType, string eventName, bool isDynamic)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                _handlers.Add(eventName, new List<SubscriptionInfo>());
            }

            if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
            {
                throw new ArgumentException(
                    $"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
            }

            _logger.LogDebug("{Id} Add new subscription {typeName}. Current handlers count {count}", _guid, handlerType.Name, _handlers[eventName].Count);

            if (isDynamic)
            {
                _handlers[eventName].Add(SubscriptionInfo.Dynamic(handlerType));
            }
            else
            {
                _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
            }
        }

        public void RemoveSubscription<T, TH>(string? key)
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent
        {
            var eventName = key ?? this.GetEventKey<T>();
            var handlerToRemove = DoFindSubscriptionToRemove(eventName, typeof(TH));

            DoRemoveHandler(eventName, handlerToRemove);
        }


        private void DoRemoveHandler(string eventName, SubscriptionInfo subsToRemove)
        {
            if (subsToRemove != null)
            {
                _handlers[eventName].Remove(subsToRemove);
                if (!_handlers[eventName].Any())
                {
                    _handlers.Remove(eventName);
                    _eventTypes.Remove(eventName);
                    RaiseOnEventRemoved(eventName);
                }

            }
        }

        public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName)
        {
            _logger.LogDebug("{Id} Get subscriptions of {eventName}.", _guid, eventName);
            if (_handlers.ContainsKey(eventName)) return _handlers[eventName];
            foreach (var key in _handlers.Keys)
            {
                if (RabbitMQUtils.IsTopicMatch(eventName, key))
                {
                    return _handlers[key];
                }
            }
            return Array.Empty<SubscriptionInfo>();
        }

        private void RaiseOnEventRemoved(string eventName)
        {
            var handler = OnEventRemoved;
            handler?.Invoke(this, eventName);
        }

        private SubscriptionInfo DoFindSubscriptionToRemove(string eventName, Type handlerType)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                return null;
            }
            var subscription = GetHandlersForEvent(eventName);
            return subscription.SingleOrDefault(s => s.HandlerType == handlerType);

        }

        public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName)
            || _handlers.Keys.Any(key => RabbitMQUtils.IsTopicMatch(eventName, key));

        public Type GetEventTypeByName(string eventName)
        {
            if (_eventTypes.ContainsKey(eventName))
            {
                return _eventTypes[eventName];
            }
            foreach (var key in _eventTypes.Keys)
            {
                if (RabbitMQUtils.IsTopicMatch(eventName, key))
                {
                    return _eventTypes[key];
                }
            }
            return default;
        }

        public string GetEventKey(Type type)
        {
            return type.Name;
        }

    }
}
