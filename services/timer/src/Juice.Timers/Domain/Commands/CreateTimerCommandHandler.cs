using Juice.MediatR;

namespace Juice.Timers.Domain.Commands
{
    public class CreateTimerCommandHandler
        : IRequestHandler<CreateTimerCommand, TimerRequest>
    {
        private ITimerRepository _repository;

        private TimerManager _timer;

        public CreateTimerCommandHandler(ITimerRepository repository, TimerManager timer)
        {
            _repository = repository;
            _timer = timer;
        }

        public async Task<TimerRequest> Handle(CreateTimerCommand request, CancellationToken cancellationToken)
        {
            var timerRequest = new TimerRequest(request.Issuer, request.CorrelationId, request.AbsoluteExpired);
            var id = await _repository.CreateAsync(timerRequest, cancellationToken);

            await _timer.StartAsync(timerRequest);

            return timerRequest;
        }
    }

    // Use for Idempotency in Command process
    public class CreateTimerIdentifiedCommandHandler
        : IdentifiedCommandHandler<CreateTimerCommand, TimerRequest>
    {
        private ITimerRepository _repository;

        public CreateTimerIdentifiedCommandHandler(IMediator mediator,
            ITimerRepository timerRepository,
            IRequestManager requestManager, ILogger<CreateTimerIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
            _repository = timerRepository;
        }

        protected override async Task<TimerRequest> CreateResultForDuplicateRequestAsync(IdentifiedCommand<CreateTimerCommand, TimerRequest> message)
        {
            var timerRequest = await _repository.GetByCorrelationIdAsync(message.Command.CorrelationId, default);

            if (timerRequest.IsExpired && !timerRequest.IsCompleted)
            {
                await _mediator.Send(new IdentifiedCommand<CompleteTimerCommand, IOperationResult>(new CompleteTimerCommand(timerRequest.Id), timerRequest.Id));
            }
            return timerRequest;
        }

        protected override (string IdProperty, string CommandId) ExtractInfo(CreateTimerCommand command)
            => (nameof(command.CorrelationId), command.CorrelationId);
    }
}
