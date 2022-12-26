using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class WorkflowFinishedDomainEvent : INotification
    {
        public NodeContext Node { get; init; }
        public WorkflowStatus Status { get; init; }

        public WorkflowFinishedDomainEvent(NodeContext node, WorkflowStatus status)
        {
            Node = node;
            Status = status;
        }
    }
}
