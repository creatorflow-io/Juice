namespace Juice.Workflows.Domain.Commands
{
    public interface INodeCommand
    {
        public NodeContext Node { get; }
        public string WorkflowId { get; }
        public string? CorrelationId { get; }
    }
}
