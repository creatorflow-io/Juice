namespace Juice.Workflows.Domain.Commands
{
    public class StartServiceTaskCommand : IRequest<IOperationResult>
    {
        public string WorkflowId { get; init; }
        public string? CorrelationId { get; init; }
        public NodeContext Node { get; init; }

        public StartServiceTaskCommand(string workflowId, string? correlationId, NodeContext node)
        {
            WorkflowId = workflowId;
            CorrelationId = correlationId;
            Node = node;
        }
    }
}
