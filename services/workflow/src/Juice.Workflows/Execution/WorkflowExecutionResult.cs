namespace Juice.Workflows.Execution
{
    public class WorkflowExecutionResult
    {
        public WorkflowStatus Status { get; set; }
        public string? Message { get; set; }
        public WorkflowContext Context { get; set; }
        public bool IsExecuted { get; set; }
    }
}
