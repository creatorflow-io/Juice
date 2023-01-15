namespace Juice.Timers
{
    public interface ITimer : IDisposable
    {
        Guid Id { get; }
        bool IsCompleted { get; }
        Task StartAsync(TimerRequest request);
        Task CancelAsync();
    }
}
