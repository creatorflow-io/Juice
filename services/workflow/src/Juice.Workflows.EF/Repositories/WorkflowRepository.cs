namespace Juice.Workflows.EF.Repositories
{
    internal class WorkflowRepository : IWorkflowRepository
    {
        private readonly WorkflowDbContext _dbContext;
        public WorkflowRepository(WorkflowDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OperationResult> CreateAsync(WorkflowRecord workflow, CancellationToken token)
        {
            try
            {
                if (await _dbContext.WorkflowDefinitions.AnyAsync(d => d.Id == workflow.Id))
                {
                    return OperationResult.Failed(default, "Workflow is already exists.");
                }
                _dbContext.WorkflowRecords.Add(workflow);
                await _dbContext.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
        public Task<WorkflowRecord?> GetAsync(string workflowId, CancellationToken token)
            => _dbContext.WorkflowRecords.FirstOrDefaultAsync(d => d.Id == workflowId, token);
        public async Task<OperationResult> UpdateAsync(WorkflowRecord workflow, CancellationToken token)
        {
            try
            {
                if (!await _dbContext.WorkflowRecords.AnyAsync(d => d.Id == workflow.Id))
                {
                    return OperationResult.Failed(default, "Workflow not found.");
                }
                _dbContext.WorkflowRecords.Update(workflow);
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
