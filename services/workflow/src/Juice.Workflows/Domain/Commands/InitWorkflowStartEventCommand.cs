namespace Juice.Workflows.Domain.Commands
{
    public class InitWorkflowStartEventCommand : IRequest<IOperationResult>
    {
        public string WorkflowId { get; init; }
        public NodeRecord[] StartNodes { get; init; }

        public InitWorkflowStartEventCommand(string workflowId, NodeRecord[] startNodes)
        {
            WorkflowId = workflowId;
            StartNodes = startNodes;
        }
    }
}
