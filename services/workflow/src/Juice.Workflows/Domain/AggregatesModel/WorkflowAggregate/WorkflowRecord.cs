using System.ComponentModel.DataAnnotations;
using Juice.Domain;

namespace Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate
{
    /// <summary>
    /// Represents a workflow instance.
    /// </summary>
    public class WorkflowRecord : Entity<string>
    {

        /// <summary>
        /// A unique identifier for this workflow.
        /// </summary>
        [Key]
        public string Id { get; set; }

        public string? RefWorkflowId { get; set; }

        /// <summary>
        /// The correlation ID can be used to resume workflows that are associated with specific objects, such as content items.
        /// </summary>
        public string? CorrelationId { get; set; }

        public WorkflowStatus Status { get; set; }

        public string? FaultMessage { get; set; }
    }
}
