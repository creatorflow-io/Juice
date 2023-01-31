using Juice.EventBus;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Domain.Commands;
using MediatR;

namespace Juice.Workflows.Api.IntegrationEvents.Handlers
{
    public class TimerExpiredIntegrationEventHandler : IIntegrationEventHandler<TimerExpiredIntegrationEvent>
    {
        private IMediator _mediator;
        public TimerExpiredIntegrationEventHandler(IMediator mediator)
        {
            _mediator = mediator;
        }
        public async Task HandleAsync(TimerExpiredIntegrationEvent @event)
        {
            await _mediator.Send(new ResumeWorkflowCommand(@event.Issuer, @event.CorrelationId));
        }
    }
}
