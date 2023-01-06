namespace Juice.Workflows.Execution
{
    /// <summary>
    /// A record of flow on a workflow process
    /// </summary>
    public record FlowRecord
    {
        public FlowRecord() { }
        public FlowRecord(string id, string sourceRef, string destinationRef)
        {
            Id = id;
            SourceRef = sourceRef;
            DestinationRef = destinationRef;
        }
        public FlowRecord(string id, string sourceRef, string destinationRef, string? name, string? processId, string? conditionExpression)
        {
            Id = id;
            SourceRef = sourceRef;
            DestinationRef = destinationRef;
            Name = name;
            ProcessIdRef = processId;
            ConditionExpression = conditionExpression;
        }

        public string Id { get; init; }
        public string SourceRef { get; init; }
        public string DestinationRef { get; init; }
        public string? Name { get; init; }

        public string? ProcessIdRef { get; init; }

        public string? ConditionExpression { get; init; }
    }
}
