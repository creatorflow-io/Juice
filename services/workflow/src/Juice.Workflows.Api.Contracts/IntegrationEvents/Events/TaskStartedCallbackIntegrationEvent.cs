using Juice.EventBus;

namespace Juice.Workflows.Api.Contracts.IntegrationEvents.Events
{
    public record TaskStartedCallbackIntegrationEvent(Guid CallbackId, string TaskId) : IntegrationEvent
    {
    }
}
