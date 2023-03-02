using Juice.EventBus;

namespace Juice.Workflows.Api.Contracts.IntegrationEvents.Events
{
    public record TaskRequestIntegrationEvent(Guid CallbackId, string Key, string? CorrelationId, IDictionary<string, object?> Properties)
        : IntegrationEvent
    {
        public override string GetEventKey() => Key;
    }
}
