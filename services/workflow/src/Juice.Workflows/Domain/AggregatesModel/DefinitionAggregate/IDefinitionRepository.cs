namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public interface IDefinitionRepository
    {
        Task<OperationResult> CreateAsync(WorkflowDefinition workflowDefinition, CancellationToken token);
        Task<OperationResult> UpdateAsync(WorkflowDefinition workflowDefinition, CancellationToken token);
        Task<WorkflowDefinition?> GetAsync(string definitionId, CancellationToken token);
        Task<bool> ExistAsync(string definitionId, CancellationToken token);
    }
}
