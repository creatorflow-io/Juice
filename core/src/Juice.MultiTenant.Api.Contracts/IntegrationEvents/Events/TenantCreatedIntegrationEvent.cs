using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantCreatedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
