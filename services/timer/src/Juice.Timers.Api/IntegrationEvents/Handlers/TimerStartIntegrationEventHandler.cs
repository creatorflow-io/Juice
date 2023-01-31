using Juice.EventBus;
using Juice.MediatR;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;

namespace Juice.Timers.Api.IntegrationEvents.Handlers
{
    public class TimerStartIntegrationEventHandler : IIntegrationEventHandler<TimerStartIntegrationEvent>
    {
        private IMediator _mediator;
        private ILogger _logger;
        public TimerStartIntegrationEventHandler(ILogger<TimerStartIntegrationEventHandler> logger,
            IMediator mediator)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task HandleAsync(TimerStartIntegrationEvent @event)
        {
            var command = new CreateTimerCommand(@event.Issuer, @event.CorrelationId, @event.AbsoluteExpired);
            var rs = await _mediator.Send(new IdentifiedCommand<CreateTimerCommand, TimerRequest>(command, @event.Id));
            if (rs == null)
            {
                _logger.LogInformation("No handler found.");
            }
            else
            {
                _logger.LogInformation("Create timer " + (rs.Succeeded ? "succeeded. Timer Id: " + rs.Data?.Id : "failed. " + rs.ToString()));
            }
        }
    }
}
