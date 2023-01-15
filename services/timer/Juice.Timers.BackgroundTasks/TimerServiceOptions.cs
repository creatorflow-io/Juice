namespace Juice.Timers.BackgroundTasks
{
    public class TimerServiceOptions : TimerOptions
    {
        public int CleanupTimerAfterDays { get; set; } = 30;
        public int CleanupMinutesInterval { get; set; } = 30;
        public int ProcessingSecondsInterval { get; set; } = 5;
    }
}
