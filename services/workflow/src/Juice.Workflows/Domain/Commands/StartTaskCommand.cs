using Juice.Workflows.Nodes;

namespace Juice.Workflows.Domain.Commands
{
    public class StartTaskCommand<TTask> : IRequest<IOperationResult>
        where TTask : Activity
    {
        public string WorkflowId { get; init; }
        public string? CorrelationId { get; init; }
        public NodeContext Node { get; init; }

        public StartTaskCommand(string workflowId, string? correlationId, NodeContext node)
        {
            WorkflowId = workflowId;
            CorrelationId = correlationId;
            Node = node;
        }
    }

}
