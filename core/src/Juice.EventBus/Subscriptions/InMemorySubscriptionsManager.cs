using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Subscriptions
{
    internal class InMemorySubscriptionsManager : ISubscriptionsManager
    {

        private readonly HashSet<SubscriptionInfo> _subscriptionInfos = new();
        private readonly object _lock = new();

        // Lookups built from _subscriptionInfos on first use and dropped when a subscription is added.
        private volatile Lookups? _lookups;

        private readonly ILogger _logger;

        private Guid _guid = Guid.NewGuid();

        private bool _topicSupported;

        // Broker consumers resolve the event type to deserialize from the routing key (GetEventTypeByNameAsync), so a
        // key there must name one event type. Local dispatch already holds the event instance and never resolves a
        // type from a key, so it does not need the restriction.
        private readonly bool _keysIdentifyEventType;

        /// <param name="providers">The subscriptions to register.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="topicSupport">Match routes with <c>*</c>/<c>#</c> topic patterns.</param>
        /// <param name="keysIdentifyEventType">
        /// Reject a key already registered for another event type (the default, required where
        /// <see cref="GetEventTypeByNameAsync"/> picks the type to deserialize). The local manager passes
        /// <c>false</c>, so a route may equal another event's type name.
        /// </param>
        public InMemorySubscriptionsManager(
            IEnumerable<ISubscriptionsProvider> providers,
            ILogger logger, bool topicSupport, bool keysIdentifyEventType = true)
        {
            _logger = logger;
            _topicSupported = topicSupport;
            _keysIdentifyEventType = keysIdentifyEventType;
            foreach (var provider in providers)
            {
                foreach (var sub in provider.GetSubscriptions())
                {
                    AddSubscription(sub.EventType, sub.HandlerType, sub.IsRouted ? sub.Key : null);
                }
            }
        }

        public bool IsEmpty => _subscriptionInfos.Count == 0;

        public void AddSubscription(Type eventType, Type handlerType, string? key)
        {
            var eventName = key ?? GetDefaultEventKey(eventType);
            var isRouted = key != null;

            lock (_lock)
            {
                if (_subscriptionInfos.Any(s => s.EventType == eventType && s.HandlerType == handlerType && s.Key == eventName && s.IsRouted == isRouted))
                {
                    return;
                }

                if (_keysIdentifyEventType && _subscriptionInfos.Any(s => s.Key == eventName && s.EventType != eventType))
                {
                    throw new InvalidOperationException($"The key '{eventName}' is already registered for event type '{_subscriptionInfos.First(s => s.Key == eventName).EventType.FullName}', cannot register for event type '{eventType.FullName}'.");
                }
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("{Id} Registering subscription {typeName} for {eventName}", _guid, handlerType.Name, eventName);
                }

                _subscriptionInfos.Add(SubscriptionInfo.Typed(eventType, handlerType, key));
                _lookups = null;
            }
        }

        public ValueTask<IEnumerable<Type>> GetHandlersForEventAsync(string eventName)
        {
            _logger.LogDebug("{Id} Get subscriptions of {eventName}.", _guid, eventName);

            return ValueTask.FromResult(MatchRoute(eventName).Select(s => s.HandlerType));
        }

        public ValueTask<IEnumerable<Type>> GetHandlersForEventAsync(string eventName, Type eventType)
        {
            _logger.LogDebug("{Id} Get subscriptions of {eventName} ({eventType}).", _guid, eventName, eventType.Name);

            var lookups = GetLookups();
            // Without a route: every event the subscribed type accepts, derived types included (handlers are
            // contravariant). With a route: every route the event name matches, exact and pattern alike.
            var unrouted = lookups.UnroutedFor.GetOrAdd(eventType,
                type => lookups.Unrouted.Where(s => s.EventType.IsAssignableFrom(type)).ToArray());
            var routed = MatchAllRoutes(lookups, eventName)
                .Where(s => s.IsRouted && s.EventType.IsAssignableFrom(eventType));
            return ValueTask.FromResult(unrouted.Concat(routed).Select(s => s.HandlerType).Distinct());
        }

        public ValueTask<bool> HasSubscriptionsForEventAsync(string eventName) => ValueTask.FromResult(MatchRoute(eventName).Any());

        public ValueTask<Type?> GetEventTypeByNameAsync(string eventName)
            => ValueTask.FromResult<Type?>(MatchRoute(eventName).FirstOrDefault()?.EventType);

        /// <summary>
        /// The broker lookup (routing key only): the exact key if registered, else the matching patterns. Kept as it was
        /// for the RabbitMQ and NATS consumers, whose bindings and filter subjects already decide what arrives.
        /// </summary>
        private IEnumerable<SubscriptionInfo> MatchRoute(string eventName)
        {
            var byKey = GetLookups().ByKey;
            if (byKey.TryGetValue(eventName, out var exact)) { return exact; }
            if (!_topicSupported) { return []; }

            return byKey
                .Where(kv => RoutingKeyUtils.IsTopicMatch(eventName, kv.Key))
                .SelectMany(kv => kv.Value);
        }

        /// <summary>Every subscription whose key matches <paramref name="eventName"/>: the exact key and the patterns.</summary>
        private IEnumerable<SubscriptionInfo> MatchAllRoutes(Lookups lookups, string eventName)
        {
            var exact = lookups.ByKey.TryGetValue(eventName, out var subs) ? subs : [];
            if (!_topicSupported) { return exact; }

            return exact.Concat(lookups.ByKey
                .Where(kv => kv.Key != eventName && RoutingKeyUtils.IsTopicMatch(eventName, kv.Key))
                .SelectMany(kv => kv.Value));
        }

        private Lookups GetLookups()
        {
            var lookups = _lookups;
            if (lookups != null) { return lookups; }
            lock (_lock)
            {
                return _lookups ??= new Lookups(
                    _subscriptionInfos
                        .GroupBy(s => s.Key)
                        .ToDictionary(g => g.Key, g => g.ToList()),
                    _subscriptionInfos.Where(s => !s.IsRouted).ToList());
            }
        }

        private string GetDefaultEventKey(Type type)
        {
            return type.Name;
        }

        private sealed record Lookups(
            Dictionary<string, List<SubscriptionInfo>> ByKey,
            List<SubscriptionInfo> Unrouted)
        {
            /// <summary>The unrouted subscriptions that accept each event type, memoized per type.</summary>
            public ConcurrentDictionary<Type, SubscriptionInfo[]> UnroutedFor { get; } = new();
        }

    }
}
