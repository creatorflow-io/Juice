namespace Juice.Workflows.Domain.Commands
{
    public class StartWorkflowCommand : IRequest<IOperationResult>, IWorkflowCommand
    {
        public StartWorkflowCommand(string workflowId, string? correlationId, string? name, Dictionary<string, object?>? parameters = default)
        {
            WorkflowId = workflowId;
            CorrelationId = correlationId;
            Name = name;
            Parameters = parameters;
        }

        public string WorkflowId { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? Name { get; private set; }
        public Dictionary<string, object?>? Parameters { get; private set; }
    }
}
