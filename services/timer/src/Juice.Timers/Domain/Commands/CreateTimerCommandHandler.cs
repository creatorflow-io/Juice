using Juice.MediatR;

namespace Juice.Timers.Domain.Commands
{
    public class CreateTimerCommandHandler
        : IRequestHandler<CreateTimerCommand, TimerRequest>
    {
        private ITimerRepository _repository;

        public CreateTimerCommandHandler(ITimerRepository repository)
        {
            _repository = repository;
        }

        public async Task<TimerRequest> Handle(CreateTimerCommand request, CancellationToken cancellationToken)
        {
            var timerRequest = new TimerRequest(request.Issuer, request.CorrelationId, request.AbsoluteExpired);
            await _repository.CreateAsync(timerRequest, cancellationToken);
            return timerRequest;
        }
    }

    // Use for Idempotency in Command process
    public class CreateTimerIdentifiedCommandHandler
        : IdentifiedCommandHandler<CreateTimerCommand, TimerRequest>
    {
        private ITimerRepository _repository;

        public CreateTimerIdentifiedCommandHandler(
            IMediator mediator,
            ITimerRepository timerRepository,
            IRequestManager requestManager, ILogger<CreateTimerIdentifiedCommandHandler> logger)
            : base(mediator, requestManager, logger)
        {
            _repository = timerRepository;
        }

        protected override async Task<TimerRequest?> CreateResultForDuplicateRequestAsync(IdentifiedCommand<CreateTimerCommand, TimerRequest> message)
        {
            var timerRequest = await _repository.GetByCorrelationIdAsync(message.Command.CorrelationId, default);
            if (timerRequest == null)
            {
                return await _mediator.Send(new CreateTimerCommand(message.Command.Issuer, message.Command.CorrelationId, message.Command.AbsoluteExpired));
            }
            return timerRequest;
        }

        protected override (string IdProperty, string CommandId) ExtractInfo(CreateTimerCommand command)
            => (nameof(command.CorrelationId), command.CorrelationId);
    }
}
