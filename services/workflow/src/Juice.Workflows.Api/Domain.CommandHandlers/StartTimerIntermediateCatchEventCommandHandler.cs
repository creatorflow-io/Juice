using Juice.EventBus;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Nodes.Events;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartTimerIntermediateCatchEventCommandHandler : StartTimerCommandHandlerBase<TimerIntermediateCatchEvent>
    {
        public StartTimerIntermediateCatchEventCommandHandler(IEventBus eventBus, IEventRepository eventRepository) : base(eventBus, eventRepository)
        {
        }
    }
}
