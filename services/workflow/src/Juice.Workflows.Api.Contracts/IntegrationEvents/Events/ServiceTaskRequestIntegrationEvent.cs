using Juice.EventBus;

namespace Juice.Workflows.Api.Contracts.IntegrationEvents.Events
{
    public record ServiceTaskRequestIntegrationEvent(Guid CallbackId, string? CorrelationId, IDictionary<string, object?> Properties)
        : IntegrationEvent
    {

    }
}
