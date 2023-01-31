using Juice.Integrations.EventBus;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Timers.EF;

namespace Juice.Timers.Api.Domain.EventHandlers
{
    public class TimerExpiredDomainEventHandler : INotificationHandler<TimerExpiredDomainEvent>
    {
        private IIntegrationEventService<TimerDbContext> _integrationService;
        private readonly ILoggerFactory _logger;
        public TimerExpiredDomainEventHandler(ILoggerFactory logger,
            IIntegrationEventService<TimerDbContext> integrationService)
        {
            _logger = logger;
            _integrationService = integrationService;
        }
        public async Task Handle(TimerExpiredDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TimerExpiredDomainEventHandler>()
                .LogTrace("Timer {Identifier} has been completed. Delayed: {Delayed}",
                    notification.Request.Id, DateTimeOffset.Now - notification.Request.AbsoluteExpired);

            var @event = new TimerExpiredIntegrationEvent(notification.Request.Issuer, notification.Request.CorrelationId, notification.Request.AbsoluteExpired);
            await _integrationService.AddAndSaveEventAsync(@event);
        }
    }
}
