using Juice.EventBus;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Api.IntegrationEvents.Handlers;

namespace Juice.Workflows.Api.DependencyInjection
{
    public static class WfApiEventBusExtensions
    {
        public static void InitWorkflowIntegrationEvents(this IEventBus eventBus)
        {
            eventBus.Subscribe<TimerExpiredIntegrationEvent, TimerExpiredIntegrationEventHandler>();

            eventBus.Subscribe<MessageCatchIntegrationEvent, MessageCatchIntegrationEventHandler>("wfcatch.*.#");
        }
    }
}
