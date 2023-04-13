using System.Threading;
using Juice.Workflows.Domain.Commands;

namespace Juice.Workflows.Tests.Domain.CommandHandlers
{
    internal class StartSendTaskCommandHandler :
        IRequestHandler<StartTaskCommand<SendTask>, IOperationResult>
    {
        private EventQueue _queue;
        private ILogger _logger;
        public StartSendTaskCommandHandler(ILogger<StartSendTaskCommandHandler> logger,
            EventQueue eventQueue)
        {
            _logger = logger;
            _queue = eventQueue;
        }

        public async Task<IOperationResult> Handle(StartTaskCommand<SendTask> request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handle command");
            _queue.Throw(request.Node.Record.Id);
            return OperationResult.Success;
        }
    }
}
