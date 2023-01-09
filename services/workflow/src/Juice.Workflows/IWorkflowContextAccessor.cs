namespace Juice.Workflows
{
    public interface IWorkflowContextAccessor
    {
        public string? WorkflowId { get; }
        public WorkflowContext? Context { get; }
        public void SetContext(WorkflowContext context);
        public void SetWorkflowId(string workflowId);
    }

    internal class WorkflowContextAccessor : IWorkflowContextAccessor
    {
        public string? WorkflowId { get; private set; }
        private WorkflowContext? _context;
        public WorkflowContext? Context => _context;
        public void SetContext(WorkflowContext context)
        {
            if (context == null) { throw new ArgumentNullException("context"); }
            if (_context != null) { return; }
            _context = context;
        }
        public void SetWorkflowId(string workflowId)
        {
            if (workflowId == null) { throw new ArgumentNullException("workflowId"); }
            if (WorkflowId != null) { return; }
            WorkflowId = workflowId;
        }
    }
}
