namespace Juice.Workflows.EF.Repositories
{
    internal class DefinitionRepository : IDefinitionRepository
    {
        private readonly WorkflowDbContext _dbContext;
        public DefinitionRepository(WorkflowDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<OperationResult> CreateAsync(WorkflowDefinition workflowDefinition, CancellationToken token)
        {
            try
            {
                if (await _dbContext.WorkflowDefinitions.AnyAsync(d => d.Id == workflowDefinition.Id))
                {
                    return OperationResult.Failed(default, "Workflow is already exists.");
                }
                _dbContext.WorkflowDefinitions.Add(workflowDefinition);
                await _dbContext.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public async Task<OperationResult> DeleteAsync(string definitionId, CancellationToken token)
        {
            try
            {
                var definition = await _dbContext.WorkflowDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId);
                if (definition != null)
                {
                    definition.ClearData();
                    _dbContext.WorkflowDefinitions.Remove(definition);
                    await _dbContext.SaveChangesAsync(token);
                }
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
        public Task<bool> ExistAsync(string definitionId, CancellationToken token)
            => _dbContext.WorkflowDefinitions.AnyAsync(d => d.Id == definitionId, token);
        public Task<WorkflowDefinition?> GetAsync(string definitionId, CancellationToken token)
            => _dbContext.WorkflowDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, token);
        public async Task<OperationResult> UpdateAsync(WorkflowDefinition workflowDefinition, CancellationToken token)
        {
            try
            {
                if (!await _dbContext.WorkflowDefinitions.AnyAsync(d => d.Id == workflowDefinition.Id))
                {
                    return OperationResult.Failed(default, "Workflow not found.");
                }
                _dbContext.WorkflowDefinitions.Update(workflowDefinition);
                await _dbContext.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
