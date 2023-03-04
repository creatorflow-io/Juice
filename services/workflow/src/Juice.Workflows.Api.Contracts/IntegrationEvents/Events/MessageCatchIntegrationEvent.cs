using Juice.EventBus;

namespace Juice.Workflows.Api.Contracts.IntegrationEvents.Events
{
    public record MessageCatchIntegrationEvent(string Key, Guid? CallbackId,
        string? CorrelationId, bool IsCompleted,
        Dictionary<string, object?> Properties) : IntegrationEvent
    {
        public override string GetEventKey() => Key;
    }
}
