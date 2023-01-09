namespace Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate
{
    public class ProcessSnapshot
    {
        private string _workflowId;
        public string WorkflowId => _workflowId;
        public string Id { get; init; }
        public string? Name { get; init; }

        private WorkflowStatus _status;
        public WorkflowStatus Status { get { return _status; } init { _status = value; } }
        public void SetStatus(WorkflowStatus status)
        {
            _status = status;
        }
    }
}
