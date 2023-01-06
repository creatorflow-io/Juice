namespace Juice.Workflows.InMemory
{
    internal class InMemoryWorkflowRepository : IWorkflowRepository
    {
        private Dictionary<string, WorkflowRecord> _workflowRecords = new Dictionary<string, WorkflowRecord>();
        public Task<OperationResult> CreateAsync(WorkflowRecord workflow, CancellationToken token)
        {
            _workflowRecords[workflow.Id] = workflow;
            return Task.FromResult(OperationResult.Success);
        }

        public Task<WorkflowRecord?> GetAsync(string workflowId, CancellationToken token)
        {
            return Task.FromResult(_workflowRecords.ContainsKey(workflowId) ? _workflowRecords[workflowId] : null);
        }

        public Task<OperationResult> UpdateAsync(WorkflowRecord workflow, CancellationToken token)
        {
            _workflowRecords[workflow.Id] = workflow;
            return Task.FromResult(OperationResult.Success);
        }

    }
}
