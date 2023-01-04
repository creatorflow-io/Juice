namespace Juice.Workflows.Builder
{
    public interface IWorkflowContextBuilder
    {
        Task<bool> ExistsAsync(string workflowId, CancellationToken token);
        Task<WorkflowContext> BuildAsync(string workflowId,
            string instanceId,
            Dictionary<string, object?>? input,
            CancellationToken token);
    }
}
