using Juice.EventBus;

namespace Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events
{
    public record TenantDeletedIntegrationEvent(string TenantIdentifier, string? TenantName) : IntegrationEvent;
}
