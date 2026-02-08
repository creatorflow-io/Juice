[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Juice.EventBus.Tests")]
namespace Juice.Messaging.Policies.Internal
{
    internal sealed class PublishingPolicyOptions
    {
        public PublishRule Default { get; init; } = new();
        public List<PublishRule> Rules { get; init; } = new();
    }

    internal sealed class PublisherDestination
    {
        public string Key { get; init; } = default!;
        public string Destination { get; init; } = default!;
    }

    internal sealed record PublishRule
    {
        public int Priority { get; init; }
        public PublishRuleMatch Match { get; init; } = new();
        public List<PublisherDestination> Publishers { get; init; } = new();
    }

    internal sealed record PublishRuleMatch
    {
        public string? Event { get; init; }
        public string? Domain { get; init; }
        public string? TenantIdentifier { get; init; }
        public string? TenantTier { get; init; }
    }
}
