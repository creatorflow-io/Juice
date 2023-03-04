using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Nodes.Activities;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartSendTaskCommandHandler : StartTaskCommandHandlerBase<SendTask>
    {
        public StartSendTaskCommandHandler(IEventBus eventBus, IEventRepository eventRepository) : base(eventBus, eventRepository)
        {

        }

        public override async Task<IOperationResult> Handle(StartTaskCommand<SendTask> request, CancellationToken cancellationToken)
        {
            try
            {
                var callbackEvent = new EventRecord(request.WorkflowId, request.Node.Record.Id, false, request.CorrelationId, request.Node.Record.Name);
                var callbackEventRs = await _eventRepository.CreateUniqueByWorkflowAsync(callbackEvent, cancellationToken);
                if (!callbackEventRs.Succeeded)
                {
                    return callbackEventRs;
                }

                var properties = request.Node.GetSharedProperties();

                var @event = new MessageThrowIntegrationEvent(GetThrowEventKey(request), callbackEvent.Id, request.CorrelationId, properties);

                await _eventBus.PublishAsync(@event);

                callbackEvent.Complete();
                await _eventRepository.UpdateAsync(callbackEvent, cancellationToken);

                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

    }
}
