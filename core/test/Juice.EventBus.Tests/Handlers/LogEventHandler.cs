
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Tests.Handlers
{
    internal class LogEventHandler : IIntegrationEventHandler<LogEvent>
    {
        private ILogger _logger;
        private readonly HandledService _handledService;

        public LogEventHandler(ILogger<LogEventHandler> logger, HandledService handledService)
        {
            _logger = logger;
            _handledService = handledService;
        }

        public async Task HandleAsync(LogEvent @event)
        {
            await Task.Delay(200);
            _logger.LogInformation("[X] Received {0} at {1}", @event.GetEventKey(), @event.CreationDate);
            _handledService.Handle(nameof(LogEventHandler), @event.Id);
        }
    }
}
