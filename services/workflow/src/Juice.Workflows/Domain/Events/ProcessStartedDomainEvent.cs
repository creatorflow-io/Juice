using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class ProcessStartedDomainEvent : INotification
    {
        public NodeContext Node { get; init; }
        public ProcessStartedDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
