
namespace Juice.EventBus
{
    public interface IMultiTenantIntegrationEvent: IIntegrationEvent
    {
        string? TenantId { get; }
    }
}
