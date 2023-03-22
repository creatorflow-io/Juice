namespace Juice.Workflows.EF.Repositories
{
    internal class DefinitionRepository<TContext> : IDefinitionRepository
        where TContext : DbContext
    {
        private readonly TContext _dbContext;
        public virtual IQueryable<WorkflowDefinition> WorkflowDefinitions => _dbContext.Set<WorkflowDefinition>();
        public DefinitionRepository(TContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<OperationResult> CreateAsync(WorkflowDefinition workflowDefinition, CancellationToken token)
        {
            try
            {
                if (await WorkflowDefinitions.AnyAsync(d => d.Id == workflowDefinition.Id))
                {
                    return OperationResult.Failed(default, "Workflow is already exists.");
                }
                _dbContext.Add(workflowDefinition);
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
                var definition = await WorkflowDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId);
                if (definition != null)
                {
                    definition.ClearData();
                    _dbContext.Remove(definition);
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
            => WorkflowDefinitions.AnyAsync(d => d.Id == definitionId, token);
        public Task<WorkflowDefinition?> GetAsync(string definitionId, CancellationToken token)
            => WorkflowDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, token);
        public async Task<OperationResult> UpdateAsync(WorkflowDefinition workflowDefinition, CancellationToken token)
        {
            try
            {
                if (!await WorkflowDefinitions.AnyAsync(d => d.Id == workflowDefinition.Id))
                {
                    return OperationResult.Failed(default, "Workflow not found.");
                }
                _dbContext.Update(workflowDefinition);
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
