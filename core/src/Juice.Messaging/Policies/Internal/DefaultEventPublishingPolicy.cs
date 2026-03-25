using Microsoft.Extensions.Options;

namespace Juice.Messaging.Policies.Internal
{
    /// <summary>
    /// Tracks all <see cref="IMessagePublishingPolicy"/> contributor types registered via
    /// any <c>AddPublishingPolicies</c> overload. Accumulated during DI registration;
    /// resolved at container-build time by <see cref="CompositeMessagePublishingPolicy"/>.
    /// Uses insertion-order deduplication so that config + code overloads both sharing
    /// <see cref="DefaultEventPublishingPolicy"/> result in a single entry.
    /// </summary>
    internal sealed class PolicyContributorRegistry
    {
        private readonly List<Type> _types = new();

        /// <summary>Adds <paramref name="type"/> if not already present.</summary>
        public void TryAdd(Type type)
        {
            if (!_types.Contains(type))
                _types.Add(type);
        }

        public IReadOnlyList<Type> Types => _types;
    }

    /// <summary>
    /// Composes all registered <see cref="IMessagePublishingPolicy"/> contributors.
    /// Routes from every policy are merged and returned together (fan-out).
    /// </summary>
    internal sealed class CompositeMessagePublishingPolicy : IMessagePublishingPolicy
    {
        private readonly IReadOnlyList<IMessagePublishingPolicy> _policies;

        public CompositeMessagePublishingPolicy(IReadOnlyList<IMessagePublishingPolicy> policies)
        {
            _policies = policies;
        }

        public async ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
        {
            if (_policies.Count == 1)
                return await _policies[0].ResolveAsync(context);

            var routes = new List<PublishRoute>();
            foreach (var policy in _policies)
                routes.AddRange(await policy.ResolveAsync(context));
            return routes;
        }
    }

    internal sealed class DefaultEventPublishingPolicy
    : IMessagePublishingPolicy
    {
        private readonly PublishingPolicyOptions _options;
        public DefaultEventPublishingPolicy(
            IOptions<PublishingPolicyOptions> options)
        {
            _options = options.Value;
        }

        public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
        {
            var rule = _options.Rules
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.IsCodeDefined ? 1 : 0)
                .FirstOrDefault(r => r.Match.IsMatch(context));
            if(rule != null)
            {
                // 3️⃣ Matched Rule
                return Map(rule.Publishers);
            }
            // 4️⃣ Default
            return Map(_options.Default.Publishers);
        }

        private static ValueTask<IReadOnlyCollection<PublishRoute>> Map(
            IEnumerable<PublisherDestination> publishers)
        {
            IReadOnlyCollection<PublishRoute> routes = [.. publishers.Select(p => new PublishRoute(p.Key, p.Destination, p.RoutingKey))];
            return ValueTask.FromResult(routes);
        }
    }
}
