using Juice.Messaging.Policies;
using Microsoft.Extensions.Options;

namespace Juice.Messaging.Policies.Internal
{
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
            IReadOnlyCollection<PublishRoute> routes = [.. publishers.Select(p => new PublishRoute(p.Key, p.Destination))];
            return ValueTask.FromResult(routes);
        }
    }
}
