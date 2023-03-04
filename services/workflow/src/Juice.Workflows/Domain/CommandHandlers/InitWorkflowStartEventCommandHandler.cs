using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Domain.Commands;

namespace Juice.Workflows.Domain.CommandHandlers
{
    public class InitWorkflowStartEventCommandHandler : IRequestHandler<InitWorkflowStartEventCommand, IOperationResult>
    {
        private readonly IEventRepository _eventRepository;
        public InitWorkflowStartEventCommandHandler(IEventRepository eventRepository)
        {
            _eventRepository = eventRepository;
        }
        public async Task<IOperationResult> Handle(InitWorkflowStartEventCommand request, CancellationToken cancellationToken)
        {
            return await _eventRepository.UpdateStartNodesAsync(request.WorkflowId,
                request.StartNodes.Select(n => new EventRecord(request.WorkflowId, n.Id, true, default, n.Name)).ToArray(),
                cancellationToken);
        }
    }
}
