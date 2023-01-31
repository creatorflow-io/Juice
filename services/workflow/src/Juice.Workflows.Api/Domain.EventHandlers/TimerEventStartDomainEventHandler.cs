using Juice.Extensions;
using Juice.Integrations.EventBus;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Domain.Events;
using Juice.Workflows.EF;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Workflows.Api.Domain.EventHandlers
{
    public class TimerEventStartDomainEventHandler : INotificationHandler<TimerEventStartDomainEvent>
    {
        private IIntegrationEventService<WorkflowPersistDbContext> _integrationService;
        private readonly ILoggerFactory _logger;
        public TimerEventStartDomainEventHandler(ILoggerFactory logger,
            IIntegrationEventService<WorkflowPersistDbContext> integrationService)
        {
            _logger = logger;
            _integrationService = integrationService;
        }

        public async Task Handle(TimerEventStartDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TimerEventStartDomainEventHandler>()
                .LogTrace("Timer {Name} has been sent",
                    notification.Node.DisplayName);

            var after = notification.Node.Properties.GetOption<TimeSpan?>("After")
                ?? TimeSpan.FromHours(2);

            var @event = new TimerStartIntegrationEvent(notification.WorkflowId, notification.Node.Record.Id, DateTimeOffset.Now.Add(after));

            await _integrationService.AddAndSaveEventAsync(@event);
        }
    }
}
