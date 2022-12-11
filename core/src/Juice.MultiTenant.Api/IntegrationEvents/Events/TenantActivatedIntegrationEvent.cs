using Juice.EventBus;

namespace Juice.MultiTenant.Api.IntegrationEvents.Events
{
    public record TenantActivatedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
