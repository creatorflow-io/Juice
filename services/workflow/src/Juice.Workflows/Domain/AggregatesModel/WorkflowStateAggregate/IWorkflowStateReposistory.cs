namespace Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate
{
    public interface IWorkflowStateReposistory
    {
        Task<OperationResult> PersistAsync(string workflowId, WorkflowState state, CancellationToken token);

        Task<WorkflowState> GetAsync(string workflowId, CancellationToken token);
    }
}
