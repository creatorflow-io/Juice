using Juice.MediatR;

namespace Juice.Timers.Domain.Commands
{
    public class CompleteTimerCommandHandler : IRequestHandler<CompleteTimerCommand, IOperationResult>
    {
        private ITimerRepository _repository;
        private TimerManager _timer;
        private ILogger _logger;
        public CompleteTimerCommandHandler(ILogger<CompleteTimerCommandHandler> logger,
            ITimerRepository repository, TimerManager timer)
        {
            _logger = logger;
            _repository = repository;
            _timer = timer;
        }
        public async Task<IOperationResult> Handle(CompleteTimerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var timerRequest = await _repository.GetAsync(request.TimerRequestId, cancellationToken);
                if (timerRequest == null)
                {
                    _logger.LogWarning("Timer {Id} not found", request.TimerRequestId);
                    return OperationResult.Failed("Timer not found");
                }
                if (timerRequest.IsCompleted)
                {
                    return OperationResult.Success;
                }
                timerRequest.Complete();
                await _repository.UpdateAsync(timerRequest, cancellationToken);
                _timer.TryRemove(timerRequest.Id);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex.StackTrace);
                return OperationResult.Failed(ex);
            }
        }
    }

    // Use for Idempotency in Command process
    public class CompleteTimerIdentifiedCommandHandler
        : IdentifiedCommandHandler<CompleteTimerCommand>
    {

        public CompleteTimerIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<CompleteTimerIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
        }

        protected override Task<IOperationResult?> CreateResultForDuplicateRequestAsync(IdentifiedCommand<CompleteTimerCommand> message)
            => Task.FromResult(default(IOperationResult));

        protected override (string IdProperty, string CommandId) ExtractInfo(CompleteTimerCommand command)
            => (nameof(command.TimerRequestId), command.TimerRequestId.ToString());

    }
}
