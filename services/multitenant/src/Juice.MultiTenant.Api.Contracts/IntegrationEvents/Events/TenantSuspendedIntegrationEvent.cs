using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantSuspendedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;

}
