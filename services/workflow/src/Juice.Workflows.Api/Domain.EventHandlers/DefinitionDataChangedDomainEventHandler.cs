using Juice.Workflows.Domain.Commands;
using Juice.Workflows.Domain.Events;
using MediatR;

namespace Juice.Workflows.Api.Domain.EventHandlers
{
    public class DefinitionDataChangedDomainEventHandler : INotificationHandler<DefinitionDataChangedDomainEvent>
    {
        private IMediator _mediator;
        public DefinitionDataChangedDomainEventHandler(IMediator mediator)
        {
            _mediator = mediator;
        }
        public async Task Handle(DefinitionDataChangedDomainEvent notification, CancellationToken cancellationToken)
        {
            var startNodes = notification.Nodes.Where(n => n.IsStart).Select(n => n.NodeRecord).ToArray();
            await _mediator.Send(new InitWorkflowStartEventCommand(notification.WorkflowDefinition.Id, startNodes));
        }
    }
}
