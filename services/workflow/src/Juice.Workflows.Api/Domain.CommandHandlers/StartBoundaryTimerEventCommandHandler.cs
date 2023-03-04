using Juice.EventBus;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Nodes.Events;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartBoundaryTimerEventCommandHandler : StartTimerCommandHandlerBase<BoundaryTimerEvent>
    {
        public StartBoundaryTimerEventCommandHandler(IEventBus eventBus, IEventRepository eventRepository) : base(eventBus, eventRepository)
        {
        }
    }
}
