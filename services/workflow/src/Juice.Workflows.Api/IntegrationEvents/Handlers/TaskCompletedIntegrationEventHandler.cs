using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.Commands;
using MediatR;

namespace Juice.Workflows.Api.IntegrationEvents.Handlers
{
    public class TaskCompletedIntegrationEventHandler : IIntegrationEventHandler<TaskCompletedCallbackIntegrationEvent>
    {
        private IMediator _mediator;
        public TaskCompletedIntegrationEventHandler(IMediator mediator)
        {
            _mediator = mediator;
        }
        public async Task HandleAsync(TaskCompletedCallbackIntegrationEvent @event)
        {
            await _mediator.Send(new DispatchWorkflowEventCommand(@event.CallbackId, true, new Dictionary<string, object?> {
                { "TaskId", @event.TaskId },
                { "TaskState", @event.State }
            }));
        }
    }
}
