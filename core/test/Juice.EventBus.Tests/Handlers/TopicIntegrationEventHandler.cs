using System.Threading.Tasks;
using Juice.EventBus.Tests.Events;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Tests.Handlers
{
    internal class TopicIntegrationEventHandler : IIntegrationEventHandler<TopicIntegrationEvent>
    {
        private ILogger _logger;
        private readonly HandledService _handledService;

        public TopicIntegrationEventHandler(ILogger<TopicIntegrationEventHandler> logger, HandledService handledService)
        {
            _logger = logger;
            _handledService = handledService;
        }

        public async Task HandleAsync(TopicIntegrationEvent @event)
        {
            await Task.Delay(200);
            _logger.LogInformation("[X] Received {0} at {1}", @event.EventName, @event.CreatedAt);
            _handledService.Handle(nameof(TopicIntegrationEventHandler), @event.MessageId);
        }
    }

    internal class TopicIntegrationEventHandler1 : IIntegrationEventHandler<TopicIntegrationEvent>
    {
        private ILogger _logger;
        private readonly HandledService _handledService;

        public TopicIntegrationEventHandler1(ILogger<TopicIntegrationEventHandler1> logger, HandledService handledService)
        {
            _logger = logger;
            _handledService = handledService;
        }

        public async Task HandleAsync(TopicIntegrationEvent @event)
        {
            await Task.Delay(200);
            _logger.LogInformation("[X] Received {0} at {1}", @event.EventName, @event.CreatedAt);
            _handledService.Handle(nameof(TopicIntegrationEventHandler1), @event.MessageId);
        }
    }
}
