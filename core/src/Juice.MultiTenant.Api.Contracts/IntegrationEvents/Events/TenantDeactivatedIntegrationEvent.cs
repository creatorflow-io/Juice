using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantDeactivatedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
