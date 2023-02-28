namespace Juice.Workflows.Domain.Commands
{
    public class ResumeWorkflowCommand : IRequest<IOperationResult>, IWorkflowCommand
    {
        public ResumeWorkflowCommand(string workflowId, string nodeId, Dictionary<string, object?>? parameters = default)
        {
            WorkflowId = workflowId;
            NodeId = nodeId;
            Parameters = parameters;
        }

        public string WorkflowId { get; private set; }
        public string NodeId { get; private set; }
        public Dictionary<string, object?>? Parameters { get; private set; }
    }
}
