using Juice.EventBus;
using Juice.Extensions;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using MediatR;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartTimerCommandHandler : IRequestHandler<StartTimerCommand, IOperationResult>
    {
        private readonly IEventBus _eventBus;
        private readonly IEventRepository _eventRepository;
        public StartTimerCommandHandler(IEventBus eventBus, IEventRepository eventRepository)
        {
            _eventBus = eventBus;
            _eventRepository = eventRepository;
        }
        public async Task<IOperationResult> Handle(StartTimerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var after = request.Node.Properties.GetOption<TimeSpan?>("After")
                    ?? TimeSpan.FromHours(2);

                var callbackEvent = new EventRecord(request.WorkflowId, request.Node.Record.Id, false, request.CorrelationId, request.Node.Record.Name);
                var callbackEventRs = await _eventRepository.CreateAsync(callbackEvent, cancellationToken);
                if (!callbackEventRs.Succeeded)
                {
                    return callbackEventRs;
                }

                var @event = new TimerStartIntegrationEvent("workflow", callbackEvent.Id.ToString(), DateTimeOffset.Now.Add(after));

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
