using System.ComponentModel.DataAnnotations;
using Juice.Domain;

namespace Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate
{
    /// <summary>
    /// Represents a workflow instance.
    /// </summary>
    public class WorkflowRecord : Entity<string>
    {
        public WorkflowRecord() { }
        public WorkflowRecord(string id, string definitionId, string? correlationId, string? name)
        {
            Id = id;
            DefinitionId = definitionId;
            CorrelationId = correlationId;
            Name = name;
        }
        /// <summary>
        /// A unique identifier for this workflow.
        /// </summary>
        [Key]
        public string Id { get; set; }

        public string DefinitionId { get; set; }

        public string? User { get; set; }

        /// <summary>
        /// The correlation ID can be used to resume workflows that are associated with specific objects, such as content items.
        /// </summary>
        public string? CorrelationId { get; set; }

        public WorkflowStatus Status { get; set; }

        public string? FaultMessage { get; set; }

    }
}
