using Juice.EventBus;
using Juice.Extensions;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Nodes;
using MediatR;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public abstract class StartTaskCommandHandlerBase<TTask> : StartCatchCommandHandlerBase<StartTaskCommand<TTask>>,
        IRequestHandler<StartTaskCommand<TTask>, IOperationResult>
        where TTask : Activity
    {
        protected readonly IEventBus _eventBus;
        protected readonly IEventRepository _eventRepository;
        public StartTaskCommandHandlerBase(IEventBus eventBus, IEventRepository eventRepository)
            : base(eventRepository)
        {
            _eventBus = eventBus;
            _eventRepository = eventRepository;
        }

        public override async Task<IOperationResult> Handle(StartTaskCommand<TTask> request, CancellationToken cancellationToken)
        {
            try
            {
                var catchKey = GetCatchEventKey(request);
                var callbackEvent = new EventRecord(request.WorkflowId, request.Node.Record.Id, false,
                                            request.CorrelationId, catchKey, request.Node.Record.Name);
                var callbackEventRs = await _eventRepository.CreateUniqueByWorkflowAsync(callbackEvent, cancellationToken);
                if (!callbackEventRs.Succeeded)
                {
                    return callbackEventRs;
                }

                var properties = request.Node.GetSharedProperties();
                var @event = new MessageThrowIntegrationEvent(GetThrowEventKey(request), callbackEvent.Id, request.CorrelationId, properties);

                await _eventBus.PublishAsync(@event);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        protected virtual string GetThrowEventKey(StartTaskCommand<TTask> request)
        {

            var provider = request.Node.Properties.GetOption<string?>("Provider") ?? "general";

            var taskName = typeof(TTask).Name.ToLower();

            return $"wfthrow.{taskName}.{provider}";
        }
    }

}
