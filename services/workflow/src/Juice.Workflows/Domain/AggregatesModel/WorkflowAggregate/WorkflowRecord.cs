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
        public string Id { get; init; }

        public string DefinitionId { get; init; }

        /// <summary>
        /// The correlation ID can be used to resume workflows that are associated with specific objects, such as content items.
        /// </summary>
        public string? CorrelationId { get; init; }

        public DateTimeOffset? StatusLastUpdate { get; private set; }
        public WorkflowStatus Status { get; private set; }

        public string? FaultMessage { get; private set; }

        public void UpdateStatus(WorkflowStatus status, string? message)
        {
            Status = status;
            StatusLastUpdate = DateTimeOffset.Now;
            if (status == WorkflowStatus.Faulted && !string.IsNullOrEmpty(message))
            {
                FaultMessage = message;
            }
        }

    }
}
