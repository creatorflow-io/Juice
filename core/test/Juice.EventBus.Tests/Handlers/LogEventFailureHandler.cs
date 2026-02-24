using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Tests.Handlers
{
    internal class LogEventFailureHandler : Messaging.IIntegrationEventHandler<LogEvent>
    {
        private ILogger _logger;
        private readonly HandledService _handledService;

        public LogEventFailureHandler(ILogger<LogEventFailureHandler> logger, HandledService handledService)
        {
            _logger = logger;
            _handledService = handledService;
        }

        public async Task HandleAsync(LogEvent @event)
        {
            _logger.LogInformation("[X] Received {0} at {1}", @event.EventName, @event.CreatedAt);
            _handledService.Handle(nameof(LogEventFailureHandler), @event.MessageId);
            throw new InvalidOperationException("Simulated failure in LogEventFailureHandler");
        }
    }
}
