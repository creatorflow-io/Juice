using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Workflows.Api.IntegrationEvents.Handlers
{
    public class MessageCatchIntegrationEventHandler : IIntegrationEventHandler<MessageCatchIntegrationEvent>
    {
        private IMediator _mediator;
        private IEventRepository _eventRepository;
        private ILogger _logger;
        public MessageCatchIntegrationEventHandler(IMediator mediator, IEventRepository eventRepository
            , ILogger<MessageCatchIntegrationEventHandler> logger)
        {
            _mediator = mediator;
            _eventRepository = eventRepository;
            _logger = logger;
        }
        public async Task HandleAsync(MessageCatchIntegrationEvent @event)
        {
            if (!@event.CallbackId.HasValue && string.IsNullOrEmpty(@event.CorrelationId))
            {
                throw new ArgumentException("@event must consits CallbackId or CorrelationId info");
            }
            if (@event.CallbackId.HasValue)
            {
                await _mediator.Send(new DispatchWorkflowEventCommand(@event.CallbackId.Value, @event.IsCompleted, @event.Properties));
            }
            else if (!string.IsNullOrEmpty(@event.CorrelationId))
            {
                var pendingEvents = await _eventRepository.FindAllAsync(e => !e.IsCompleted
                // not required correlationId on start node 
                    && ((e.IsStartEvent && string.IsNullOrEmpty(e.CorrelationId)) || e.CorrelationId == @event.CorrelationId)
                    && e.CatchingKey == @event.Key,
                    default);
                if (pendingEvents.Any())
                {
                    var processedWorkflowIds = new HashSet<string>();

                    foreach (var pendingEvent in pendingEvents)
                    {
                        if (pendingEvent.IsStartEvent)
                        {
                            await _mediator.Send(new DispatchWorkflowEventCommand(pendingEvent.Id, @event.IsCompleted, @event.Properties));
                        }
                        else
                        {
                            if (processedWorkflowIds.Add(pendingEvent.WorkflowId))
                            {
                                var rs = await _mediator.Send(new DispatchWorkflowEventCommand(pendingEvent.Id, @event.IsCompleted, @event.Properties));
                                if (!rs.Succeeded)
                                {
                                    processedWorkflowIds.Remove(pendingEvent.WorkflowId);
                                }
                            }
                            else // duplicated event
                            {
                                if (@event.IsCompleted) { pendingEvent.Complete(); }
                                else { pendingEvent.MarkCalled(); }
                                await _eventRepository.UpdateAsync(pendingEvent, default);
                            }
                        }
                    }
                }
                else if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("No pending event found with CatchingKey {CatchingKey} and CorrelationId {CorrelationId}", @event.Key, @event.CorrelationId);
                }
            }
        }
    }
}
