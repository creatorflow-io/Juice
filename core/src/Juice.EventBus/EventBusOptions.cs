namespace Juice.EventBus
{
    public class EventBusOptions
    {
        public const string ConfigSection = "Juice:EventBus";

        public string? Connection { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public int RetryCount { get; set; }
    }
}
