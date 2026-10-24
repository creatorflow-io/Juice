using System.Text.RegularExpressions;

namespace Juice.EventBus.NATS.Policies
{
    /// <summary>
    /// Parses retry tiers such as <c>10s</c> or RabbitMQ tier routing keys such as <c>content.retry.1m</c>:
    /// the last dot-separated token is the delay.
    /// </summary>
    internal static class RetryTierParser
    {
        private static readonly Regex _pattern = new(@"^(\d+)(ms|s|m|h)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static TimeSpan Parse(string tier)
        {
            var token = tier?.Split('.').LastOrDefault();
            var match = string.IsNullOrEmpty(token) ? null : _pattern.Match(token);
            if (match == null || !match.Success || !long.TryParse(match.Groups[1].Value, out var value))
            {
                throw new FormatException($"Invalid retry tier '{tier}'.");
            }
            return match.Groups[2].Value switch
            {
                "ms" => TimeSpan.FromMilliseconds(value),
                "s" => TimeSpan.FromSeconds(value),
                "m" => TimeSpan.FromMinutes(value),
                _ => TimeSpan.FromHours(value)
            };
        }
    }
}
