namespace Juice.Workflows.Domain.Commands
{
    public class DispatchWorkflowEventCommand : IRequest<IOperationResult>
    {
        public Guid EventRecordId { get; init; }
        public bool IsCompleted { get; init; }
        public Dictionary<string, object?>? Options { get; init; }

        public DispatchWorkflowEventCommand(Guid eventRecordId, bool isCompleted, Dictionary<string, object?>? options)
        {
            EventRecordId = eventRecordId;
            IsCompleted = isCompleted;
            Options = options;
        }
    }
}
