using Juice.EventBus.Publishing.Policies;
using Microsoft.Extensions.Options;

namespace Juice.EventBus.Publishing.Policies.Internal
{
    internal sealed class DefaultEventPublishingPolicy
    : IEventPublishingPolicy
    {
        private readonly PublishingPolicyOptions _options;
        public DefaultEventPublishingPolicy(
            IOptions<PublishingPolicyOptions> options)
        {
            _options = options.Value;
        }

        public ValueTask<IReadOnlyCollection<EventPublishRoute>> ResolveAsync(PolicyResolveContext context)
        {
            var rule = _options.Rules
                .OrderByDescending(r => r.Priority)
                .FirstOrDefault(r => r.Match.IsMatch(context));
            if(rule != null)
            {
                // 3️⃣ Matched Rule
                return Map(rule.Publishers);
            }
            // 4️⃣ Default
            return Map(_options.Default.Publishers);
        }

        private static ValueTask<IReadOnlyCollection<EventPublishRoute>> Map(
            IEnumerable<PublisherDestination> publishers)
        {
            IReadOnlyCollection<EventPublishRoute> routes = [.. publishers.Select(p => new EventPublishRoute(p.Key, p.Destination))];
            return ValueTask.FromResult(routes);
        }
    }
}
