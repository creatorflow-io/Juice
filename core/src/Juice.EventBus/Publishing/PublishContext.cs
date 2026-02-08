namespace Juice.EventBus.Publishing
{
    public sealed record PublishContext(
        string MessageId,
        string? Destination = null,
        string? TenantId = null,
        IDictionary<string, object?>? Headers = default);
}
