namespace Juice.Workflows.Execution
{
    public class NodeExecutionResult
    {
        public WorkflowStatus Status { get; private set; }

        public IEnumerable<string> Outcomes { get; private set; }
        public string? Message { get; set; }

        public static readonly NodeExecutionResult Empty = new(WorkflowStatus.Idle, Array.Empty<string>());

        public static readonly NodeExecutionResult Halted = new(WorkflowStatus.Halted, Array.Empty<string>());

        public NodeExecutionResult(string message, WorkflowStatus status, params string[] outcomes)
        {
            Message = message;
            Status = status;
            Outcomes = outcomes;
        }

        public NodeExecutionResult(WorkflowStatus status, params string[] outcomes)
        {
            Status = status;
            Outcomes = outcomes;
        }

        public static NodeExecutionResult Fault(string message)
            => new(WorkflowStatus.Faulted, Array.Empty<string>()) { Message = message };
    }
}
