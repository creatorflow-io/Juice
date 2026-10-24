using System.Diagnostics.Metrics;

namespace Juice.EventBus.NATS
{
    internal static class JetStreamMetrics
    {
        private static readonly Meter _meter = new(
            name: "infra.nats.metrics",
            version: "1.0.0");

        private static readonly Counter<long> PublishErrorCounter =
            _meter.CreateCounter<long>(
                "jetstream_publish_error_total",
                description: "Total JetStream publish errors");

        private static readonly Counter<long> ConsumeRetryCounter =
            _meter.CreateCounter<long>(
                "jetstream_consume_retry_total",
                description: "Total JetStream messages scheduled for redelivery after a handler failure");

        private static readonly Counter<long> ConsumeParkedCounter =
            _meter.CreateCounter<long>(
                "jetstream_consume_parked_total",
                description: "Total JetStream messages moved to a parking stream");

        public static void IncrementPublishError(string subject)
        {
            PublishErrorCounter.Add(
                1,
                new KeyValuePair<string, object?>("subject", subject));
        }

        public static void IncrementRetry(string destination)
        {
            ConsumeRetryCounter.Add(
                1,
                new KeyValuePair<string, object?>("destination", destination));
        }

        public static void IncrementParked(string destination)
        {
            ConsumeParkedCounter.Add(
                1,
                new KeyValuePair<string, object?>("destination", destination));
        }
    }
}
