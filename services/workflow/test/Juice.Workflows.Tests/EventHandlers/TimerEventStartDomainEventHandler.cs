using System.Threading;
using Juice.Workflows.Domain.Events;

namespace Juice.Workflows.Tests.EventHandlers
{
    internal class TimerEventStartDomainEventHandler : INotificationHandler<TimerEventStartDomainEvent>
    {
        private EventQueue _queue;
        public TimerEventStartDomainEventHandler(EventQueue queue)
        {
            _queue = queue;
        }
        public async Task Handle(TimerEventStartDomainEvent notification, CancellationToken cancellationToken)
        {
            await Task.Delay(1000);
            _queue.Throw(notification.Node.Record.Id);
        }
    }
}
