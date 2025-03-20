
namespace Juice.EventBus
{
    public interface IMultiTenantIntegrationEvent
    {
        string? TenantId { get; }
    }
}
