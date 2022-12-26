namespace Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate
{
    public interface IWorkflowRepository
    {
        Task<OperationResult> CreateAsync(WorkflowRecord workflow, CancellationToken token);
        Task<OperationResult> UpdateAsync(WorkflowRecord workflow, CancellationToken token);
        Task<WorkflowRecord?> GetAsync(string workflowId, CancellationToken token);

    }
}
