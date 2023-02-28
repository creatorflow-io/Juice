using Juice.Workflows.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Workflows.Api.Domain.EventHandlers
{
    public class TimerEventStartDomainEventHandler : INotificationHandler<TimerEventStartDomainEvent>
    {
        private readonly ILoggerFactory _logger;
        public TimerEventStartDomainEventHandler(ILoggerFactory logger)
        {
            _logger = logger;
        }

        public async Task Handle(TimerEventStartDomainEvent notification, CancellationToken cancellationToken)
        {
            var logger = _logger.CreateLogger<TimerEventStartDomainEventHandler>();
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Timer {Name} has been sent",
                    notification.Node.DisplayName);
            }
        }
    }
}
