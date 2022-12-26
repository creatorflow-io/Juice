using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class ServiceTaskRequestDomainEvent : INotification
    {
        public NodeContext Node { get; init; }

        public ServiceTaskRequestDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
