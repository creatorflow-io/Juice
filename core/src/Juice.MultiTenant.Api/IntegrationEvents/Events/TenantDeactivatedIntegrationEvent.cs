using Juice.EventBus;

namespace Juice.MultiTenant.Api.IntegrationEvents.Events
{
    public record TenantDeactivatedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
