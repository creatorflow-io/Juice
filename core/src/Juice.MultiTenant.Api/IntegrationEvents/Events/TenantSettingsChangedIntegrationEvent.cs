using Juice.EventBus;

namespace Juice.MultiTenant.Api.IntegrationEvents.Events
{
    public record TenantSettingsChangedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
