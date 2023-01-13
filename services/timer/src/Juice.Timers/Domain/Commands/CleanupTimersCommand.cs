namespace Juice.Timers.Domain.Commands
{
    public class CleanupTimersCommand : IRequest<IOperationResult>
    {
        public DateTimeOffset Before { get; private set; }
        public CleanupTimersCommand(DateTimeOffset beforeTime)
        {
            Before = beforeTime;
        }
    }
}
