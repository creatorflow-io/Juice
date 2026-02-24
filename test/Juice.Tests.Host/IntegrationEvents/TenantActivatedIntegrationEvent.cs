using Juice.Messaging;

namespace Juice.Tests.Host.IntegrationEvents
{
    public record TenantActivatedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
