using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantSettingsChangedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
