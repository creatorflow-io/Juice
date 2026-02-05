namespace Juice.EventBus.Dispatching
{
    public sealed record EventDispatchContext(
        IEnumerable<Type> Handlers,
        string? EventName = null,
        string? TenantId = null);
}
