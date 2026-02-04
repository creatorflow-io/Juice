namespace Juice.EventBus.Dispatching
{
    public sealed record EventDispatchContext(
        string? EventName = null,
        string? TenantId = null);
}
