namespace Juice.EventBus.Extensions
{
    public static class TimeSpanNamingExtensions
    {
        public static string ToShortName(this TimeSpan timeSpan)
        {
            if (timeSpan.TotalMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timeSpan));

            if (timeSpan.TotalHours >= 1 && timeSpan.TotalHours % 1 == 0)
                return $"{(int)timeSpan.TotalHours}h";

            if (timeSpan.TotalMinutes >= 1 && timeSpan.TotalMinutes % 1 == 0)
                return $"{(int)timeSpan.TotalMinutes}m";

            if (timeSpan.TotalSeconds >= 1 && timeSpan.TotalSeconds % 1 == 0)
                return $"{(int)timeSpan.TotalSeconds}s";

            return $"{(int)timeSpan.TotalMilliseconds}ms";
        }
    }
}
