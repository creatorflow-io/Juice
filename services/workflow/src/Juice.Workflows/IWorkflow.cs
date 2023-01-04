namespace Juice.Workflows
{
    public interface IWorkflow
    {
        WorkflowContext? ExecutedContext { get; }
        Task<OperationResult<WorkflowExecutionResult>> StartAsync(string workflowId,
            string? correlationId, string? name,
            Dictionary<string, object?>? parameters,
            CancellationToken token = default);
        Task<OperationResult<WorkflowExecutionResult>> ResumeAsync(string workflowId,
            string nodeId,
            Dictionary<string, object?>? parameters,
            CancellationToken token = default);

    }

}
