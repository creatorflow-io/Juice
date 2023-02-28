using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.Commands;
using MediatR;

namespace Juice.Workflows.Api.IntegrationEvents.Handlers
{
    public class TaskStartedIntegrationEventHandler : IIntegrationEventHandler<TaskStartedCallbackIntegrationEvent>
    {
        private IMediator _mediator;
        public TaskStartedIntegrationEventHandler(IMediator mediator)
        {
            _mediator = mediator;
        }
        public async Task HandleAsync(TaskStartedCallbackIntegrationEvent @event)
        {
            await _mediator.Send(new DispatchWorkflowEventCommand(@event.CallbackId, false, new Dictionary<string, object?> {
                { "TaskId", @event.TaskId },
            }));
        }
    }
}
