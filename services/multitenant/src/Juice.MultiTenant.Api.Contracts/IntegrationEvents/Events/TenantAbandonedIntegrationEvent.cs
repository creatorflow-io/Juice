using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantAbandonedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
