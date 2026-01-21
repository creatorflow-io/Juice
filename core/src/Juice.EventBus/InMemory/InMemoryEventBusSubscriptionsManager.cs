using Microsoft.Extensions.Logging;

namespace Juice.EventBus
{
    public class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
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
        public void Clear() => _handlers.Clear();

        public async ValueTask AddSubscriptionAsync<T, TH>(string? key)
            where T : IIntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = key ?? this.GetDefaultEventKey<T>();

            await DoAddSubscriptionAsync(typeof(TH), eventName, isDynamic: false);

            _eventTypes[eventName] = typeof(T);
        }

        private ValueTask DoAddSubscriptionAsync(Type handlerType, string eventName, bool isDynamic)
        {
            if (!_handlers.ContainsKey(eventName))
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
            return ValueTask.CompletedTask;
        }

        public async ValueTask RemoveSubscriptionAsync<T, TH>(string? key)
            where TH : IIntegrationEventHandler<T>
            where T : IIntegrationEvent
        {
            var eventName = key ?? this.GetDefaultEventKey<T>();
            var handlerToRemove = await FindSubscriptionToRemoveAsync(eventName, typeof(TH));

            DoRemoveHandler(eventName, handlerToRemove);
        }

        private void DoRemoveHandler(string eventName, SubscriptionInfo? subsToRemove)
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

        public ValueTask<IEnumerable<SubscriptionInfo>> GetHandlersForEventAsync(string eventName)
        {
            _logger.LogDebug("{Id} Get subscriptions of {eventName}.", _guid, eventName);
            if (_handlers.ContainsKey(eventName)) { return ValueTask.FromResult(_handlers[eventName].AsEnumerable()); }
            if(!_topicSupported) { return ValueTask.FromResult(Array.Empty<SubscriptionInfo>().AsEnumerable()); }

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

        private void RaiseOnEventRemoved(string eventName)
        {
            var handler = OnEventRemoved;
            handler?.Invoke(this, eventName);
        }

        private async ValueTask<SubscriptionInfo?> FindSubscriptionToRemoveAsync(string eventName, Type handlerType)
        {
            if (!await HasSubscriptionsForEventAsync(eventName))
            {
                return null;
            }
            var subscription = await GetHandlersForEventAsync(eventName);
            return subscription.SingleOrDefault(s => s.HandlerType == handlerType);

        }

        public ValueTask<bool> HasSubscriptionsForEventAsync(string eventName) => ValueTask.FromResult(_handlers.ContainsKey(eventName)
            || (_topicSupported && _handlers.Keys.Any(key => RoutingKeyUtils.IsTopicMatch(eventName, key))));

        public ValueTask<Type?> GetEventTypeByNameAsync(string eventName)
        {
            if (_eventTypes.ContainsKey(eventName))
            {
                return ValueTask.FromResult<Type?>(_eventTypes[eventName]);
            }
            if(!_topicSupported) { ValueTask.FromResult<Type?>(default); }
            foreach (var key in _eventTypes.Keys)
            {
                if (RoutingKeyUtils.IsTopicMatch(eventName, key))
                {
                    return ValueTask.FromResult<Type?>(_eventTypes[key]);
                }
            }
            return ValueTask.FromResult<Type?>(default);
        }

        public string GetDefaultEventKey(Type type)
        {
            return type.Name;
        }

    }
}
