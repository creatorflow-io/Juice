using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class ProcessFinishedDomainEvent : INotification
    {
        public NodeContext Node { get; init; }
        public WorkflowStatus Status { get; init; }

        public ProcessFinishedDomainEvent(NodeContext node, WorkflowStatus status)
        {
            Node = node;
            Status = status;
        }
    }
}
