namespace Juice.Timers.Domain.Commands
{
    public record CreateTimerCommand(string Issuer, string CorrelationId, DateTimeOffset AbsoluteExpired)
        : IRequest<TimerRequest>
    { }
}
