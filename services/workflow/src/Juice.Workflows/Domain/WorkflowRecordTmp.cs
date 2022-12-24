namespace Juice.Workflows.Domain
{
    /// <summary>
    /// Represents a workflow instance.
    /// </summary>
    public class WorkflowRecordTmp
    {

        /// <summary>
        /// A unique identifier for this workflow.
        /// </summary>
        public string WorkflowId { get; set; }

        public string RefWorkflowId { get; set; }

        /// <summary>
        /// The correlation ID can be used to resume workflows that are associated with specific objects, such as content items.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// State of the workflow.
        /// </summary>
        public WorkflowState State { get; set; } = new WorkflowState();

        public WorkflowStatus Status { get; set; }

        public string? FaultMessage { get; set; }

        /// <summary>
        /// List of activities the current workflow instance is waiting on
        /// for continuing its process.
        /// </summary>
        public List<BlockingNode> Blocking { get; } = new List<BlockingNode>();


        /// <summary>
        /// The list of faulted activities.
        /// </summary>
        public List<FaultedNode> Faulted { get; } = new List<FaultedNode>();

        public DateTimeOffset CreatedDate { get; set; }

        public DateTimeOffset LastUpdate { get; set; }
    }
}
