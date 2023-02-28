namespace Juice.Workflows.InMemory
{
    internal class InMemorDefinitionRepository : IDefinitionRepository
    {
        private IDictionary<string, WorkflowDefinition> _definitions = new Dictionary<string, WorkflowDefinition>();
        public InMemorDefinitionRepository()
        {

        }
        public Task<OperationResult> CreateAsync(WorkflowDefinition workflowDefinition, CancellationToken token)
        {
            _definitions[workflowDefinition.Id] = workflowDefinition;
            return Task.FromResult(OperationResult.Success);
        }

        public Task<OperationResult> DeleteAsync(string definitionId, CancellationToken token)
        {
            if (_definitions.ContainsKey(definitionId))
            {
                _definitions.Remove(definitionId);
            }
            return Task.FromResult(OperationResult.Success);
        }
        public Task<bool> ExistAsync(string definitionId, CancellationToken token)
            => Task.FromResult(_definitions.ContainsKey(definitionId));
        public Task<WorkflowDefinition?> GetAsync(string definitionId, CancellationToken token)
            => Task.FromResult(_definitions.ContainsKey(definitionId) ? _definitions[definitionId] : default);
        public Task<OperationResult> UpdateAsync(WorkflowDefinition workflowDefinition, CancellationToken token)
        {
            _definitions[workflowDefinition.Id] = workflowDefinition;
            return Task.FromResult(OperationResult.Success);
        }
    }
}
