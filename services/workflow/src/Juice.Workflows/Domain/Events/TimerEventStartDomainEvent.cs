using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class TimerEventStartDomainEvent : INotification
    {
        public NodeContext Node { get; init; }
        public TimerEventStartDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
