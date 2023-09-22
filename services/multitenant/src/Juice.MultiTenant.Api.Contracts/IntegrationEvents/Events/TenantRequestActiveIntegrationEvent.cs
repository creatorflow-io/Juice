using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantRequestActiveIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
