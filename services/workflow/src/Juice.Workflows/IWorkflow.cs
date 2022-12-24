namespace Juice.Workflows
{
    public interface IWorkflow
    {
        WorkflowContext? ExecutedContext { get; }
        Task<OperationResult<WorkflowExecutionResult>> StartAsync(string workflowId, string? correlationId, CancellationToken token = default);
        Task<OperationResult<WorkflowExecutionResult>> ResumeAsync(string workflowId,
            string nodeId, CancellationToken token = default);

    }

}
