namespace Juice.Workflows.Domain.Events
{
    public class DefinitionDataChangedDomainEvent : INotification
    {
        public WorkflowDefinition WorkflowDefinition { get; init; }

        public IEnumerable<ProcessRecord> Processes { get; init; }
        public IEnumerable<NodeData> Nodes { get; init; }
        public IEnumerable<FlowData> Flows { get; init; }

        public DefinitionDataChangedDomainEvent(WorkflowDefinition workflowDefinition, IEnumerable<ProcessRecord> processes, IEnumerable<NodeData> nodes, IEnumerable<FlowData> flows)
        {
            this.WorkflowDefinition = workflowDefinition;
            this.Processes = processes;
            this.Nodes = nodes;
            this.Flows = flows;
        }
    }
}
