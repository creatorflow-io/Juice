using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using MediatR;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartUserTaskCommandHandler : IRequestHandler<StartUserTaskCommand, IOperationResult>
    {
        private readonly IEventBus _eventBus;
        private readonly IEventRepository _eventRepository;
        public StartUserTaskCommandHandler(IEventBus eventBus, IEventRepository eventRepository)
        {
            _eventBus = eventBus;
            _eventRepository = eventRepository;
        }

        public async Task<IOperationResult> Handle(StartUserTaskCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var callbackEvent = new EventRecord(request.WorkflowId, request.Node.Record.Id, false, request.CorrelationId, request.Node.Record.Name);
                var callbackEventRs = await _eventRepository.CreateAsync(callbackEvent, cancellationToken);
                if (!callbackEventRs.Succeeded)
                {
                    return callbackEventRs;
                }
                var @event = new UserTaskRequestIntegrationEvent(callbackEvent.Id, request.CorrelationId, request.Node.Properties);

                await _eventBus.PublishAsync(@event);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
