namespace Juice.Messaging.Local.Internal
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Delivery metrics for the local-channel and local transport publisher paths.
    /// Uses the same meter name ("delivery.metrics") as the outbox delivery infrastructure
    /// so that all local and broker routes appear in unified metric output.
    /// </summary>
    internal static class LocalChannelMetrics
    {
        private static readonly Meter _meter = new(
            name: "delivery.metrics",
            version: "1.0.0");

        private static readonly Counter<long> DeliveryAttemptCounter =
            _meter.CreateCounter<long>(
                "delivery_attempt_total",
                description: "Total delivery attempts");

        private static readonly Counter<long> DeliverySuccessCounter =
            _meter.CreateCounter<long>(
                "delivery_success_total",
                description: "Total successful deliveries");

        private static readonly Counter<long> DeliveryFailureCounter =
            _meter.CreateCounter<long>(
                "delivery_failure_total",
                description: "Total failed deliveries");

        private static readonly Histogram<double> DeliveryLatencyHistogram =
            _meter.CreateHistogram<double>(
                "delivery_latency_ms",
                unit: "ms",
                description: "Delivery latency in milliseconds");

        public static void IncrementDeliveryAttempt(string publisherKey)
        {
            DeliveryAttemptCounter.Add(
                1,
                new KeyValuePair<string, object?>("publisher", publisherKey));
        }

        public static void IncrementDeliverySuccess(string publisherKey)
        {
            DeliverySuccessCounter.Add(
                1,
                new KeyValuePair<string, object?>("publisher", publisherKey));
        }

        public static void IncrementDeliveryFailure(string publisherKey, string errorType)
        {
            DeliveryFailureCounter.Add(
                1,
                new KeyValuePair<string, object?>("publisher", publisherKey),
                new KeyValuePair<string, object?>("error_type", errorType));
        }

        public static void RecordDeliveryLatency(string publisherKey, TimeSpan elapsed)
        {
            DeliveryLatencyHistogram.Record(
                elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("publisher", publisherKey));
        }
    }
}
