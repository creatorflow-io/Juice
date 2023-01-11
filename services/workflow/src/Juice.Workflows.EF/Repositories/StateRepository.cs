using Microsoft.Extensions.Logging;

namespace Juice.Workflows.EF.Repositories
{
    internal class StateRepository : IWorkflowStateRepository
    {
        private readonly WorkflowPersistDbContext _dbContext;
        private readonly ILogger _logger;
        public StateRepository(ILogger<StateRepository> logger, WorkflowPersistDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<WorkflowState> GetAsync(string workflowId, CancellationToken token)
        {
            var state = await _dbContext.WorkflowStates
                .Where(p => Microsoft.EntityFrameworkCore.EF.Property<string>(p, "WorkflowId") == workflowId)
                .Include(p => p.ProcessSnapshots)
                .Include(p => p.FlowSnapshots)
                .Include(p => p.NodeSnapshots)
                .AsSplitQuery()
                .FirstOrDefaultAsync(token);

            if (state != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("State nodes count: {Count}", state.NodeSnapshots.Count);
                _logger.LogDebug("State flows count: {Count}", state.FlowSnapshots.Count);
                _logger.LogDebug("State processes count: {Count}", state.ProcessSnapshots.Count);
            }

            return state == null
                ? new WorkflowState() : state;
        }
        public async Task<OperationResult> PersistAsync(string workflowId, WorkflowState state, CancellationToken token)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("PersistState nodes count: {Count}", state.NodeSnapshots.Count);
                    _logger.LogDebug("PersistState flows count: {Count}", state.FlowSnapshots.Count);
                    _logger.LogDebug("PersistState processes count: {Count}", state.ProcessSnapshots.Count);
                }
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
