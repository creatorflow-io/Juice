using Juice.EventBus;
using Juice.Workflows.Models;

namespace Juice.Workflows.Api.Contracts.IntegrationEvents.Events
{
    public record TaskCompletedCallbackIntegrationEvent(Guid CallbackId, string TaskId, WorkflowStatus State) : IntegrationEvent
    {
    }
}
