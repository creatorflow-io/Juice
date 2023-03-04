namespace Juice.Workflows.Domain.AggregatesModel.EventAggregate
{
    /// <summary>
    /// Record callback event to start or resume workflow
    /// </summary>
    public class EventRecord
    {
        public EventRecord() { }
        public EventRecord(string workflowId, string nodeId, bool isStart, string? correlationId, string? displayName)
        {
            WorkflowId = workflowId;
            NodeId = nodeId;
            CorrelationId = correlationId;
            IsStartEvent = isStart;
            DisplayName = displayName;
        }

        public EventRecord(string workflowId, string nodeId, bool isStart, string? correlationId, string? catchingKey, string? displayName)
            : this(workflowId, nodeId, isStart, correlationId, displayName)
        {
            CatchingKey = catchingKey;
        }

        public Guid Id { get; set; }
        public string WorkflowId { get; private set; }
        public string NodeId { get; private set; }

        public string? DisplayName { get; private set; }

        public string? CorrelationId { get; private set; }

        public string? CatchingKey { get; private set; }

        public bool IsStartEvent { get; private set; }

        public bool IsCompleted { get; private set; }

        public DateTimeOffset CreatedDate { get; init; } = DateTimeOffset.Now;
        public DateTimeOffset? LastCall { get; private set; }

        public void Complete()
        {
            IsCompleted = true;
            LastCall = DateTimeOffset.Now;
        }

        public void MarkCalled()
        {
            LastCall = DateTimeOffset.Now;
        }

        public EventRecord UpdateDisplayName(string? name)
        {
            if (DisplayName != name)
            {
                DisplayName = name;
            }
            return this;
        }
    }
}
