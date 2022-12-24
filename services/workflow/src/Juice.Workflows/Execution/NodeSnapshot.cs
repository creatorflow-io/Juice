namespace Juice.Workflows.Execution
{
    /// <summary>
    /// Snapshot node state to store or reload workflow
    /// </summary>
    public class NodeSnapshot
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public IEnumerable<string>? Outcomes { get; set; }
        public string? Message { get; set; }
        public WorkflowStatus Status { get; set; }

        public string? User { get; set; }
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
