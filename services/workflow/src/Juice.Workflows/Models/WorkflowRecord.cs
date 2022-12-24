namespace Juice.Workflows.Models
{
    /// <summary>
    /// Represents a workflow instance.
    /// </summary>
    public class WorkflowRecord
    {

        /// <summary>
        /// A unique identifier for this workflow.
        /// </summary>
        public string WorkflowId { get; set; }

        public string? RefWorkflowId { get; set; }

        /// <summary>
        /// The correlation ID can be used to resume workflows that are associated with specific objects, such as content items.
        /// </summary>
        public string? CorrelationId { get; set; }

        public WorkflowStatus Status { get; set; }

        public string? FaultMessage { get; set; }
    }
}
