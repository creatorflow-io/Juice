namespace Juice.Workflows.Services
{
    public interface IWorkflowContextResolver
    {
        /// <summary>
        /// Resolve workflow context without state data
        /// </summary>
        /// <param name="workflowId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<WorkflowContext?> ResolveAsync(string workflowId,
            CancellationToken token);

        /// <summary>
        /// Resolve workflow context and restored state data
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<WorkflowContext?> StateResolveAsync(
            string instanceId,
            Dictionary<string, object?>? input,
            CancellationToken token);
    }
}
