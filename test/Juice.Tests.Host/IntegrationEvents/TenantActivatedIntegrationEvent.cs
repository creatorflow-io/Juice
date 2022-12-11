using Juice.EventBus;

namespace Juice.Tests.Host.IntegrationEvents
{
    public record TenantActivatedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
