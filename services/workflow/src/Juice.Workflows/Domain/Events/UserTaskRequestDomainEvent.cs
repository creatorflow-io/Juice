using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class UserTaskRequestDomainEvent : INotification
    {
        public NodeContext Node { get; init; }

        public UserTaskRequestDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
