using Juice.Messaging;

namespace Juice.Tests.Host.IntegrationEvents
{
    public record TenantSettingsChangedIntegrationEvent(string TenantIdentifier) : IntegrationEvent;
}
