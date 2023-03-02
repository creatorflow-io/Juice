using Juice.EventBus;
using Juice.Extensions;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Nodes;
using MediatR;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public abstract class StartTaskCommandHandler<TTask> : IRequestHandler<StartTaskCommand<TTask>, IOperationResult>
        where TTask : Activity
    {
        private readonly IEventBus _eventBus;
        private readonly IEventRepository _eventRepository;
        public StartTaskCommandHandler(IEventBus eventBus, IEventRepository eventRepository)
        {
            _eventBus = eventBus;
            _eventRepository = eventRepository;
        }

        public async Task<IOperationResult> Handle(StartTaskCommand<TTask> request, CancellationToken cancellationToken)
        {
            try
            {
                var callbackEvent = new EventRecord(request.WorkflowId, request.Node.Record.Id, false, request.CorrelationId, request.Node.Record.Name);
                var callbackEventRs = await _eventRepository.CreateAsync(callbackEvent, cancellationToken);
                if (!callbackEventRs.Succeeded)
                {
                    return callbackEventRs;
                }

                var @event = new TaskRequestIntegrationEvent(callbackEvent.Id, GetEventKey(request), request.CorrelationId, request.Node.Properties);

                await _eventBus.PublishAsync(@event);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        protected virtual string GetEventKey(StartTaskCommand<TTask> request)
        {

            var provider = request.Node.Properties.GetOption<string?>("Provider") ?? "general";

            var taskName = typeof(TTask).Name.Replace("Task", "").ToLower();

            return $"wftask.{taskName}.{provider}";
        }
    }

}
