namespace Juice.Timers.Domain.AggregratesModel.TimerAggregrate
{
    public interface ITimerRepository
    {
        Task<Guid> CreateAsync(TimerRequest request, CancellationToken token);

        Task UpdateAsync(TimerRequest request, CancellationToken token);

        Task<TimerRequest?> GetAsync(Guid id, CancellationToken token);
        Task<TimerRequest?> GetByCorrelationIdAsync(string id, CancellationToken token);

        Task RemoveTimersBeforeAsync(DateTimeOffset dateTime, CancellationToken token);
    }
}
