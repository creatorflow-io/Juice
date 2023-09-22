using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantPropertiesChangedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
