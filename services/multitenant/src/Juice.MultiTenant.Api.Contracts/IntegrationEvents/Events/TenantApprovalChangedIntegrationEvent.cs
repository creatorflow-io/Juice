using Juice.EventBus;
using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantApprovalChangedIntegrationEvent(string TenantIdentifier, TenantStatus Status) : IntegrationEvent;
}
