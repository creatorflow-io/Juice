using System.ComponentModel.DataAnnotations.Schema;

namespace Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate
{
    /// <summary>
    /// Snapshot node state to store or reload workflow
    /// </summary>
    public class NodeSnapshot
    {
        public NodeSnapshot()
        {

        }

        public NodeSnapshot(string id, string name, WorkflowStatus status,
            string? message, string? user, IEnumerable<string>? outcomes)
        {
            Id = id;
            Name = name;
            Status = status;
            Message = message;
            User = user;
            Outcomes = outcomes;
        }
        public string Id { get; set; }
        public string Name { get; set; }
        public IEnumerable<string>? Outcomes { get; private set; }
        public string? Message { get; private set; }
        public WorkflowStatus Status { get; private set; }
        public string? User { get; private set; }

        [NotMapped]
        public bool StatusChanged { get; private set; }
        [NotMapped]
        public WorkflowStatus OriginalStatus => _originalStatus ?? Status;
        private WorkflowStatus? _originalStatus;
        public void SetStatus(WorkflowStatus status, string? message, string? user, IEnumerable<string>? outcomes)
        {
            if (Status != status)
            {
                _originalStatus = Status;
                Status = status;
                Message = message;
                User = user;
                if (status == WorkflowStatus.Finished)
                {
                    Outcomes = outcomes;
                }
                StatusChanged = true;
            }
        }

        public void Idle(string? user, string? message = default)
            => SetStatus(WorkflowStatus.Idle, message, user, default);
    }

    public static class NodeSnapshotEnumerableExtensions
    {
        public static IEnumerable<FaultedNode> GetFaultedNodes(this IEnumerable<NodeSnapshot> nodeSnapshots)
        {
            return nodeSnapshots.Where(n => n.Status == WorkflowStatus.Faulted)
                .Select(n => new FaultedNode(n.Id, n.Message, n.User));
        }

        public static IEnumerable<ExecutedNode> GetExecutedNodes(this IEnumerable<NodeSnapshot> nodeSnapshots)
        {
            return nodeSnapshots.Where(n => n.Status == WorkflowStatus.Finished)
                .Select(n => new ExecutedNode(n.Id, n.Message, n.User, n.Outcomes));
        }

        public static IEnumerable<BlockingNode> GetBlockingNodes(this IEnumerable<NodeSnapshot> nodeSnapshots)
        {
            return nodeSnapshots.Where(n => n.Status == WorkflowStatus.Halted)
                .Select(n => new BlockingNode(n.Id, n.Name));
        }

    }
}
