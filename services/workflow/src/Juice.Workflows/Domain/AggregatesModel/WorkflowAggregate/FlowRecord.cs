namespace Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate
{
    /// <summary>
    /// A record of flow on a workflow process
    /// </summary>
    public class FlowRecord
    {
        public string Id { get; set; }
        public string SourceRef { get; set; }
        public string DestinationRef { get; set; }
        public string Name { get; set; }

        public string? OwnerId { get; set; }

        public string? ConditionExpression { get; set; }
    }
}
