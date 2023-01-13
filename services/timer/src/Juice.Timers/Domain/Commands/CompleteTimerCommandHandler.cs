using Juice.MediatR;

namespace Juice.Timers.Domain.Commands
{
    public class CompleteTimerCommandHandler : IRequestHandler<CompleteTimerCommand, IOperationResult>
    {
        private ITimerRepository _repository;
        public CompleteTimerCommandHandler(ITimerRepository repository)
        {
            _repository = repository;
        }
        public async Task<IOperationResult> Handle(CompleteTimerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var timerRequest = await _repository.GetAsync(request.TimerRequestId, cancellationToken);
                if (timerRequest.IsCompleted)
                {
                    return OperationResult.Success;
                }
                timerRequest.Complete();
                await _repository.UpdateAsync(timerRequest, cancellationToken);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }

    // Use for Idempotency in Command process
    public class CompleteTimerIdentifiedCommandHandler
        : IdentifiedCommandHandler<CompleteTimerCommand, IOperationResult>
    {

        public CompleteTimerIdentifiedCommandHandler(IMediator mediator,
            IRequestManager requestManager, ILogger<CreateTimerIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
        }

        protected override async Task<IOperationResult> CreateResultForDuplicateRequestAsync(IdentifiedCommand<CompleteTimerCommand, IOperationResult> message)
        {
            return OperationResult.Success;
        }

        protected override (string IdProperty, string CommandId) ExtractInfo(CompleteTimerCommand command)
            => (nameof(command.TimerRequestId), command.TimerRequestId.ToString());
    }
}
