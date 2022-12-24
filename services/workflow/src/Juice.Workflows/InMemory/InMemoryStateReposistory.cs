namespace Juice.Workflows.InMemory
{
    internal class InMemoryStateReposistory : IWorkflowStateReposistory
    {
        private Dictionary<string, WorkflowState> _states = new Dictionary<string, WorkflowState>();

        private ILogger _logger;

        public InMemoryStateReposistory(ILogger<InMemoryStateReposistory> logger)
        {
            _logger = logger;
        }

        public Task<WorkflowState> GetAsync(string workflowId, CancellationToken token)
            => Task.FromResult(_states.ContainsKey(workflowId) ?
                new WorkflowState(
                    _states[workflowId].FlowSnapshots,
                    _states[workflowId].NodeSnapshots,
                    _states[workflowId].Output
                )
                : new WorkflowState());
        public Task<OperationResult> PersistAsync(string workflowId, WorkflowState state, CancellationToken token)
        {
            _states[workflowId] = state;
            _logger.LogInformation("Persisted workflow state {workflowId}", workflowId);
            return Task.FromResult(OperationResult.Success);
        }
    }
}
