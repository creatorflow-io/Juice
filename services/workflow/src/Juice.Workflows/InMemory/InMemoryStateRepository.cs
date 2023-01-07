using MediatR;

namespace Juice.Workflows.InMemory
{
    internal class InMemoryStateRepository : IWorkflowStateRepository
    {
        private Dictionary<string, WorkflowState> _states = new Dictionary<string, WorkflowState>();

        private ILogger _logger;
        private IMediator _mediator;

        public InMemoryStateRepository(IMediator mediator, ILogger<InMemoryStateRepository> logger)
        {
            _logger = logger;
            _mediator = mediator;
        }

        public Task<WorkflowState> GetAsync(string workflowId, CancellationToken token)
            => Task.FromResult(_states.ContainsKey(workflowId) ?
                new WorkflowState(
                    _states[workflowId].FlowSnapshots,
                    _states[workflowId].NodeSnapshots.Select(n =>
                        new NodeSnapshot(n.Id, n.Name, n.Status, n.Message, n.User, n.Outcomes)).ToList(),
                    _states[workflowId].ProcessSnapshots,
                    _states[workflowId].Output
                )
                : new WorkflowState());
        public async Task<OperationResult> PersistAsync(string workflowId, WorkflowState state, CancellationToken token)
        {
            foreach (var node in state.NodeSnapshots)
            {
                if (node.StatusChanged)
                {
                    _logger.LogInformation("{node} status changed from {original} to {status}", node.Name, node.OriginalStatus, node.Status);
                }
            }
            foreach (var evt in state.DomainEvents)
            {
                _logger.LogInformation("Publish domain event {event}", evt.GetType().Name);
                await _mediator.Publish(evt);
            }
            _states[workflowId] = state;
            _logger.LogInformation("Persisted workflow state {workflowId}", workflowId);
            return OperationResult.Success;
        }
    }
}
