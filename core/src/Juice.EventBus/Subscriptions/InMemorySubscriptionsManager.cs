using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Subscriptions
{
    internal class InMemorySubscriptionsManager : ISubscriptionsManager
    {

        private readonly HashSet<SubscriptionInfo> _subscriptionInfos = new();

        private IDictionary<string, List<SubscriptionInfo>> Handlers => _subscriptionInfos
            .GroupBy(s => s.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        private IDictionary<string, Type> EventTypes => _subscriptionInfos
            .GroupBy(s => s.Key)
            .ToDictionary(g => g.Key, g => g.First().EventType);

        private readonly ILogger _logger;

        public event EventHandler<string>? OnEventRemoved;

        private Guid _guid = Guid.NewGuid();

        private bool _topicSupported;

        public InMemorySubscriptionsManager(
            IEnumerable<ISubscriptionsProvider> providers,
            ILogger logger, bool topicSupport)
        {
            _logger = logger;
            _topicSupported = topicSupport;
            foreach (var provider in providers)
            {
                foreach (var sub in provider.GetSubscriptions())
                {
                    AddSubscription(sub.EventType, sub.HandlerType, sub.Key);
                }
            }
        }

        public bool IsEmpty => _subscriptionInfos.Count == 0;

        public void AddSubscription(Type eventType, Type handlerType, string? key)
        {
            var eventName = key ?? GetDefaultEventKey(eventType);

            if(_subscriptionInfos.Any(s => s.EventType == eventType && s.HandlerType == handlerType && s.Key == eventName))
            {
                return;
            }

            if(_subscriptionInfos.Any(s => s.Key == eventName && s.EventType != eventType))
            {
                throw new InvalidOperationException($"The key '{eventName}' is already registered for event type '{_subscriptionInfos.First(s => s.Key == eventName).EventType.FullName}', cannot register for event type '{eventType.FullName}'.");
            }
            if(_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("{Id} Registering subscription {typeName} for {eventName}", _guid, handlerType.Name, eventName);
            }

            _subscriptionInfos.Add(SubscriptionInfo.Typed(eventType, handlerType, eventName));
        }

        public ValueTask<IEnumerable<Type>> GetHandlersForEventAsync(string eventName)
        {
            _logger.LogDebug("{Id} Get subscriptions of {eventName}.", _guid, eventName);
            
            if (Handlers.ContainsKey(eventName)) { return ValueTask.FromResult(Handlers[eventName].Select(h => h.HandlerType).AsEnumerable()); }
            if (!_topicSupported) { return ValueTask.FromResult(Array.Empty<Type>().AsEnumerable()); }

            return ValueTask.FromResult(
                Handlers.Keys.SelectMany(key =>
                {
                    if (RoutingKeyUtils.IsTopicMatch(eventName, key))
                    {
                        return Handlers[key].Select(h => h.HandlerType).AsEnumerable();
                    }
                    return [];
                })
                );
        }

        public ValueTask<bool> HasSubscriptionsForEventAsync(string eventName) => ValueTask.FromResult(Handlers.ContainsKey(eventName)
            || _topicSupported && Handlers.Keys.Any(key => RoutingKeyUtils.IsTopicMatch(eventName, key)));

        public ValueTask<Type?> GetEventTypeByNameAsync(string eventName)
        {
            if (EventTypes.ContainsKey(eventName))
            {
                return ValueTask.FromResult<Type?>(EventTypes[eventName]);
            }
            if (!_topicSupported) { ValueTask.FromResult<Type?>(default); }
            foreach (var key in EventTypes.Keys)
            {
                if (RoutingKeyUtils.IsTopicMatch(eventName, key))
                {
                    return ValueTask.FromResult<Type?>(EventTypes[key]);
                }
            }
            return ValueTask.FromResult<Type?>(default);
        }

        private string GetDefaultEventKey(Type type)
        {
            return type.Name;
        }

    }
}
