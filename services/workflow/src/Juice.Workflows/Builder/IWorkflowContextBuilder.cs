namespace Juice.Workflows.Builder
{
    public interface IWorkflowContextBuilder
    {
        int Priority { get; }
        Task<bool> ExistsAsync(string workflowId, CancellationToken token);
        Task<WorkflowContext> BuildAsync(string workflowId,
            string instanceId,
            Dictionary<string, object?>? input,
            CancellationToken token);
    }
}
