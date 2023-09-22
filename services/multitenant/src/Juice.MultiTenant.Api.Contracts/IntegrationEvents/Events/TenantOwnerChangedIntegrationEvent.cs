using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantOwnerChangedIntegrationEvent(string TenantIdentifier, string? OrignalOwnerId, string? CurrentOwnerId) : IntegrationEvent;
}
