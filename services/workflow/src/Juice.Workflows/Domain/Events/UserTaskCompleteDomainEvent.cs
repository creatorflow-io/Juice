using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class UserTaskCompleteDomainEvent : INotification
    {
        public NodeContext Node { get; init; }

        public UserTaskCompleteDomainEvent(NodeContext node)
        {
            Node = node;
        }
    }
}
