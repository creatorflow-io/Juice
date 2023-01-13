using Juice.EventBus;
using Juice.MediatR;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;

namespace Juice.Timers.Api.IntegrationEvents.Handlers
{
    public class TimerStartIntegrationEventHandler : IIntegrationEventHandler<TimerStartIntegrationEvent>
    {
        private IMediator _mediator;

        public TimerStartIntegrationEventHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task HandleAsync(TimerStartIntegrationEvent @event)
        {
            var command = new CreateTimerCommand(@event.Issuer, @event.CorrelationId, @event.AbsoluteExpired);
            await _mediator.Send(new IdentifiedCommand<CreateTimerCommand, TimerRequest>(command, @event.Id));
        }
    }
}
