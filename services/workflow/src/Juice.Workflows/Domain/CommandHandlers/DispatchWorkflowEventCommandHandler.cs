using Juice.Extensions;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;

namespace Juice.Workflows.Domain.CommandHandlers
{
    public class DispatchWorkflowEventCommandHandler : IRequestHandler<DispatchWorkflowEventCommand, IOperationResult>
    {
        private IMediator _mediator;
        private readonly IEventRepository _eventRepository;
        public DispatchWorkflowEventCommandHandler(IMediator mediator, IEventRepository eventRepository)
        {
            _mediator = mediator;
            _eventRepository = eventRepository;
        }

        public async Task<IOperationResult> Handle(DispatchWorkflowEventCommand request, CancellationToken cancellationToken)
        {

            var callbackEvent = await _eventRepository.GetAsync(request.EventRecordId, cancellationToken);
            if (callbackEvent != null)
            {
                if (callbackEvent.IsStartEvent)
                {
                    var correlationId = request.Options?.GetOption<string>("CorrelationId") ?? callbackEvent.CorrelationId;
                    var rs = await _mediator.Send(new StartWorkflowCommand(callbackEvent.WorkflowId, correlationId, default, request.Options));
                    if (rs != null)
                    {
                        callbackEvent.MarkCalled();
                        await _eventRepository.UpdateAsync(callbackEvent, cancellationToken);
                        return rs;
                    }
                }
                else
                {
                    if (request.IsCompleted && callbackEvent.IsCompleted)
                    {
                        return new OperationResult<WorkflowExecutionResult>
                        {
                            Message = "Workflow callback event is already completed.",
                            Succeeded = true
                        };
                    }

                    var rs = await _mediator.Send(new ResumeWorkflowCommand(callbackEvent.WorkflowId, callbackEvent.NodeId, request.Options));
                    if (rs != null)
                    {
                        if (request.IsCompleted && rs.Succeeded && (rs.Data?.IsExecuted ?? false))
                        {
                            callbackEvent.Complete();
                        }
                        else
                        {
                            callbackEvent.MarkCalled();
                        }
                        await _eventRepository.UpdateAsync(callbackEvent, cancellationToken);
                        return rs.Succeeded && (rs.Data?.IsExecuted ?? false)
                            ? rs
                            : OperationResult.Failed<WorkflowExecutionResult>(rs.Data?.Message ?? rs.Message);
                    }
                }
                return OperationResult.Failed<WorkflowExecutionResult>($"Invalid workflow result");
            }
            else
            {
                return OperationResult.Failed<WorkflowExecutionResult>($"Workflow callback event is no longer exist. {request.EventRecordId}");
            }

        }
    }
}
