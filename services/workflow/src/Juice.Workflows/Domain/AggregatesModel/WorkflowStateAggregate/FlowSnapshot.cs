namespace Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate
{
    /// <summary>
    /// Snapshot of flow to store or reload workflow
    /// </summary>
    public class FlowSnapshot
    {
        private string _workflowId;
        public string WorkflowId => _workflowId;
        public string Id { get; set; }
        private string _name = "";
        public string Name { get { return _name; } set { _name = value ?? ""; } }
    }
}
