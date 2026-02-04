namespace Juice.EventBus.Publishing
{
    public sealed record PublishContext(
        string? Destination = null,
        string? TenantId = null);
}
