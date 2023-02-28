namespace Juice.Workflows.Domain.Commands
{
    public class StartTimerCommand : IRequest<IOperationResult>
    {
        public string WorkflowId { get; init; }
        public string? CorrelationId { get; init; }
        public NodeContext Node { get; init; }

        public StartTimerCommand(string workflowId, string? correlationId, NodeContext node)
        {
            WorkflowId = workflowId;
            CorrelationId = correlationId;
            Node = node;
        }
    }
}
