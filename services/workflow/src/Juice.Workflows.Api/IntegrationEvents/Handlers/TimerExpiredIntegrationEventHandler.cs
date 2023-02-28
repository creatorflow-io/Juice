using Juice.EventBus;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Domain.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Workflows.Api.IntegrationEvents.Handlers
{
    public class TimerExpiredIntegrationEventHandler : IIntegrationEventHandler<TimerExpiredIntegrationEvent>
    {
        private IMediator _mediator;
        private readonly ILogger _logger;
        public TimerExpiredIntegrationEventHandler(IMediator mediator,
            ILogger<TimerExpiredIntegrationEventHandler> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }
        public async Task HandleAsync(TimerExpiredIntegrationEvent @event)
        {
            if (@event.Issuer == "workflow")
            {
                if (Guid.TryParse(@event.CorrelationId, out var callbackId))
                {
                    await _mediator.Send(new DispatchWorkflowEventCommand(callbackId, true, default));
                }
                else
                {
                    _logger.LogWarning("Could not parse timer correlationId to Guid. {id}", @event.CorrelationId ?? "");
                }
            }
        }
    }
}
