using System.Text;
using Juice.EventBus.Extensions;
using Juice.EventBus.Publishing;

namespace Juice.EventBus.NATS
{
    /// <summary>
    /// Subject, stream name and filter rules. See specs/012-nats-jetstream/contracts/wire-format.md.
    /// </summary>
    internal static class JetStreamNaming
    {
        public static void ValidateToken(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be empty.", paramName);
            }
            if (value.Any(char.IsWhiteSpace) || value.Contains('*') || value.Contains('>'))
            {
                throw new ArgumentException($"'{value}' must not contain whitespace, '*' or '>'.", paramName);
            }
            if (value.Split('.').Any(string.IsNullOrEmpty))
            {
                throw new ArgumentException($"'{value}' must not contain empty tokens.", paramName);
            }
        }

        public static string BuildSubject(string destination, string routingKey)
            => $"{destination}.{routingKey}";

        public static string ResolveRoutingKey(PublishContext context)
        {
            var headers = context.Headers ?? new Dictionary<string, object?>();
            var routingKey = !string.IsNullOrEmpty(context.RoutingKey) ? context.RoutingKey
                : headers.GetHeaderString("x-routing-key")
                ?? headers.GetHeaderString("x-message-name")
                ?? headers.GetHeaderString("x-message-type");
            if (string.IsNullOrEmpty(routingKey))
            {
                throw new InvalidOperationException(
                    "Missing required header 'x-message-name' or 'x-message-type' for routing key");
            }
            return routingKey;
        }

        public static string DeriveStreamName(string destination)
        {
            var sb = new StringBuilder(destination.Length);
            foreach (var c in destination)
            {
                sb.Append(c == '.' || c == '-' || char.IsWhiteSpace(c) ? '_' : char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        public static string ParkingStreamName(string stream)
            => $"{stream}_PARKING";

        public static string DefaultParkingPrefix(string destination)
            => $"parking.{destination}";

        /// <summary>
        /// Translate a RabbitMQ-style topic route into a JetStream filter subject.
        /// A trailing <c>#</c> becomes <c>&gt;</c>; a non-trailing <c>#</c> widens the filter to <c>{destination}.&gt;</c>
        /// and leaves the exact match to the topic matching of the subscriptions manager.
        /// </summary>
        public static string ToFilterSubject(string destination, string route)
        {
            var tokens = route.Split('.');
            for (var i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "#")
                {
                    if (i != tokens.Length - 1)
                    {
                        return $"{destination}.>";
                    }
                    tokens[i] = ">";
                }
            }
            return $"{destination}.{string.Join(".", tokens)}";
        }

        public static string ExtractEventName(string subject, string destination)
        {
            var prefix = destination + ".";
            return subject.StartsWith(prefix, StringComparison.Ordinal)
                ? subject.Substring(prefix.Length)
                : subject;
        }
    }
}
