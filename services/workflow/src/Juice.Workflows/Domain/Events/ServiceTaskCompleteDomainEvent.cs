using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class ServiceTaskCompleteDomainEvent : INotification
    {
        public NodeContext Node { get; init; }

        public ServiceTaskCompleteDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
