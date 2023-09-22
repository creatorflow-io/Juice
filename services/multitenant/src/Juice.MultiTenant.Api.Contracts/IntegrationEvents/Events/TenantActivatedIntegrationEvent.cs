using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantActivatedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
