namespace Juice.Storage.BackgroundTasks
{
    public class StorageMaintainOptions
    {
        public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan CleanupAfter { get; set; } = TimeSpan.FromDays(1);
    }
}
