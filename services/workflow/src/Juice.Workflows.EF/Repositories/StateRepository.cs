namespace Juice.Workflows.EF.Repositories
{
    internal class StateRepository : IWorkflowStateRepository
    {
        private readonly WorkflowPersistDbContext _dbContext;
        public StateRepository(WorkflowPersistDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<WorkflowState> GetAsync(string workflowId, CancellationToken token)
        {
            var state = await _dbContext.WorkflowStates
                .Where(p => Microsoft.EntityFrameworkCore.EF.Property<string>(p, "WorkflowId") == workflowId)
                .Include(p => p.ProcessSnapshots)
                .Include(p => p.FlowSnapshots)
                .Include(p => p.NodeSnapshots)
                .Select(p => new { p.FlowSnapshots, p.NodeSnapshots, p.ProcessSnapshots, p.Output })
                .FirstOrDefaultAsync(token);

            return state == null
                ? new WorkflowState()
                : new WorkflowState(state.FlowSnapshots, state.NodeSnapshots, state.ProcessSnapshots, state.Output);
        }
        public async Task<OperationResult> PersistAsync(string workflowId, WorkflowState state, CancellationToken token)
        {
            try
            {
                _dbContext.Entry(state).Property("WorkflowId").CurrentValue = workflowId;

                var exists = await _dbContext.WorkflowStates.AnyAsync(s => Microsoft.EntityFrameworkCore.EF.Property<string>(s, "WorkflowId") == workflowId, token);

                if (!exists)
                {
                    _dbContext.Add(state);
                }
                else
                {
                    _dbContext.Update(state);
                }
                await _dbContext.SaveChangesAsync();
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
