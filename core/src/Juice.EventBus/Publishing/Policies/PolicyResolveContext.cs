namespace Juice.EventBus.Publishing.Policies
{
    public sealed record PolicyResolveContext
    {
        public string EventType { get; init; } = default!;
        public string? Domain { get; init; } = default!;
        public string? TenantIdentifier { get; init; }
        public string? TenantTier { get; init; }
    }
}
