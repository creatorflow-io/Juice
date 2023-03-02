using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Newtonsoft.Json;

namespace Juice.Workflows.Tests.Host.IntegrationEvents.Handlers
{
    public class TaskRequestIntegrationEventHandler : IIntegrationEventHandler<TaskRequestIntegrationEvent>
    {
        private readonly ILogger _logger;
        public TaskRequestIntegrationEventHandler(ILogger<TaskRequestIntegrationEventHandler> logger)
        {
            _logger = logger;
        }
        public Task HandleAsync(TaskRequestIntegrationEvent @event)
        {
            _logger.LogInformation("Handling a task request. Key: {key}; CallbackId: {callbackId}; CorrelationId: {correlationId}; Data: {data}",
                @event.Key, @event.CallbackId, @event.CorrelationId, JsonConvert.SerializeObject(@event.Properties));
            return Task.CompletedTask;
        }
    }
}
