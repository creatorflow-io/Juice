using Juice.EventBus;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.Nodes.Activities;

namespace Juice.Workflows.Api.Domain.CommandHandlers
{
    public class StartSendTaskCommandHandler : StartTaskCommandHandler<SendTask>
    {
        public StartSendTaskCommandHandler(IEventBus eventBus, IEventRepository eventRepository) : base(eventBus, eventRepository)
        {

        }
    }
}
