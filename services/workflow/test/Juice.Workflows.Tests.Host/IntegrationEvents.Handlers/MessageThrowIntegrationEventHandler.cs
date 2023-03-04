using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Newtonsoft.Json;

namespace Juice.Workflows.Tests.Host.IntegrationEvents.Handlers
{
    public class MessageThrowIntegrationEventHandler : IIntegrationEventHandler<MessageThrowIntegrationEvent>
    {
        private readonly ILogger _logger;
        public MessageThrowIntegrationEventHandler(ILogger<MessageThrowIntegrationEventHandler> logger)
        {
            _logger = logger;
        }
        public Task HandleAsync(MessageThrowIntegrationEvent @event)
        {
            _logger.LogInformation("Handling a message. Id: {Id}, Key: {key}; CallbackId: {callbackId}; CorrelationId: {correlationId}; Data: {data}",
                    @event.Id, @event.Key, @event.CallbackId, @event.CorrelationId, JsonConvert.SerializeObject(@event.Properties));
            return Task.CompletedTask;
        }
    }
}
