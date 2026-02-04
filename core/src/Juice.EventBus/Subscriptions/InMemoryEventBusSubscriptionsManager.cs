using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Subscriptions
{
    internal class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
    {

        private readonly Dictionary<string, List<SubscriptionInfo>> _handlers;
        private readonly Dictionary<string, Type> _eventTypes;

        private readonly ILogger _logger;

        public event EventHandler<string>? OnEventRemoved;

        private Guid _guid = Guid.NewGuid();

        private bool _topicSupported;

        public InMemoryEventBusSubscriptionsManager(ILogger logger, bool topicSupport)
        {
            _handlers = new Dictionary<string, List<SubscriptionInfo>>();
            _eventTypes = new Dictionary<string, Type>();
            _logger = logger;
            _topicSupported = topicSupport;
        }

        public bool IsEmpty => !_handlers.Keys.Any();

        public void AddSubscription(Type eventType, Type handlerType, string? key)
        {
            var eventName = key ?? GetDefaultEventKey(eventType);

            DoAddSubscription(handlerType, eventName, isDynamic: false);

            _eventTypes[eventName] = eventType;
        }

        private void DoAddSubscription(Type handlerType, string eventName, bool isDynamic)
        {
            if (!_handlers.ContainsKey(eventName))
            {
                _handlers.Add(eventName, new List<SubscriptionInfo>());
            }

            if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
            {
                return;
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
            return;
        }

        public ValueTask<IEnumerable<SubscriptionInfo>> GetHandlersForEventAsync(string eventName)
        {
            _logger.LogDebug("{Id} Get subscriptions of {eventName}.", _guid, eventName);
            if (_handlers.ContainsKey(eventName)) { return ValueTask.FromResult(_handlers[eventName].AsEnumerable()); }
            if (!_topicSupported) { return ValueTask.FromResult(Array.Empty<SubscriptionInfo>().AsEnumerable()); }

            return ValueTask.FromResult(
                _handlers.Keys.SelectMany(key =>
                {
                    if (RoutingKeyUtils.IsTopicMatch(eventName, key))
                    {
                        return _handlers[key].AsEnumerable();
                    }
                    return [];
                })
                );
        }

        public ValueTask<bool> HasSubscriptionsForEventAsync(string eventName) => ValueTask.FromResult(_handlers.ContainsKey(eventName)
            || _topicSupported && _handlers.Keys.Any(key => RoutingKeyUtils.IsTopicMatch(eventName, key)));

        public ValueTask<Type?> GetEventTypeByNameAsync(string eventName)
        {
            if (_eventTypes.ContainsKey(eventName))
            {
                return ValueTask.FromResult<Type?>(_eventTypes[eventName]);
            }
            if (!_topicSupported) { ValueTask.FromResult<Type?>(default); }
            foreach (var key in _eventTypes.Keys)
            {
                if (RoutingKeyUtils.IsTopicMatch(eventName, key))
                {
                    return ValueTask.FromResult<Type?>(_eventTypes[key]);
                }
            }
            return ValueTask.FromResult<Type?>(default);
        }

        public virtual string GetDefaultEventKey(Type type)
        {
            return type.Name;
        }

    }
}
