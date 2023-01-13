namespace Juice.Timers.Domain.Commands
{
    /// <summary>
    /// Use IdentifiedCommand<CompleteTimerCommand, IOperationResult> instead CompleteTimerCommand directly
    /// </summary>
    public class CompleteTimerCommand : IRequest<IOperationResult>
    {
        public Guid TimerRequestId { get; private set; }
        public CompleteTimerCommand(Guid id)
        {
            TimerRequestId = id;
        }
    }
}
