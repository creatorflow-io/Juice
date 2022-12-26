namespace Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate
{
    /// <summary>
    /// Snapshot of flow to store or reload workflow
    /// </summary>
    public class FlowSnapshot
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
