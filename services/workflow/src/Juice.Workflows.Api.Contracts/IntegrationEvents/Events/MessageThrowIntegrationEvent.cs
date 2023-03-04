using Juice.EventBus;

namespace Juice.Workflows.Api.Contracts.IntegrationEvents.Events
{
    public record MessageThrowIntegrationEvent(string Key, Guid? CallbackId,
        string? CorrelationId, Dictionary<string, object?> Properties)
        : IntegrationEvent
    {
        public override string GetEventKey() => Key;
    }
}
