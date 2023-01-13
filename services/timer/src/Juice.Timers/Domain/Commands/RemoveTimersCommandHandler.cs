namespace Juice.Timers.Domain.Commands
{
    public class RemoveTimersCommandHandler : IRequestHandler<CleanupTimersCommand, IOperationResult>
    {
        private ITimerRepository _repository;

        public RemoveTimersCommandHandler(ITimerRepository repository)
        {
            _repository = repository;
        }

        public async Task<IOperationResult> Handle(CleanupTimersCommand request, CancellationToken cancellationToken)
        {
            try
            {
                await _repository.RemoveTimersBeforeAsync(request.Before, cancellationToken);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
