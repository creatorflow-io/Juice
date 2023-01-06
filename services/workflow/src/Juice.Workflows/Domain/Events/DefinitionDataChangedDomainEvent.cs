using Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate;
using MediatR;

namespace Juice.Workflows.Domain.Events
{
    public class DefinitionDataChangedDomainEvent : INotification
    {
        public WorkflowDefinition WorkflowDefinition { get; init; }

        public DefinitionDataChangedDomainEvent(WorkflowDefinition workflowDefinition)
        {
            this.WorkflowDefinition = workflowDefinition;
        }
    }
}
