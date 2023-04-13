using System.Threading;
using Juice.Workflows.Domain.Commands;

namespace Juice.Workflows.Tests.Domain.CommandHandlers
{
    internal class StartBoundaryTimerEventCommandHandler :
        IRequestHandler<StartEventCommand<BoundaryTimerEvent>, IOperationResult>
    {
        private EventQueue _queue;
        private ILogger _logger;
        public StartBoundaryTimerEventCommandHandler(ILogger<StartBoundaryTimerEventCommandHandler> logger,
            EventQueue eventQueue)
        {
            _logger = logger;
            _queue = eventQueue;
        }

        public async Task<IOperationResult> Handle(StartEventCommand<BoundaryTimerEvent> request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handle command");
            _queue.Throw(request.Node.Record.Id);
            return OperationResult.Success;
        }
    }
}
