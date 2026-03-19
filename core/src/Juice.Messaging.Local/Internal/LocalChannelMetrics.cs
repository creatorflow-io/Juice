namespace Juice.Messaging.Local.Internal
{
    using System.Diagnostics.Metrics;
    using System.Threading.Channels;

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

        private static readonly Counter<long> ChannelDroppedCounter =
            _meter.CreateCounter<long>(
                "channel_dropped_total",
                description: "Messages dropped because the local channel was full");

        // Registered lazily when the channel is created so the gauge holds a live reference.
        private static bool _queueDepthRegistered;

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

        public static void IncrementChannelDropped(string publisherKey, string messageType)
        {
            ChannelDroppedCounter.Add(
                1,
                new KeyValuePair<string, object?>("publisher", publisherKey),
                new KeyValuePair<string, object?>("message_type", messageType));
        }

        /// <summary>
        /// Registers an observable gauge that reports the current number of messages
        /// waiting in the local channel queue. Called once when the channel is created.
        /// </summary>
        public static void RegisterQueueDepth(ChannelReader<ChannelEnvelope> reader)
        {
            if (_queueDepthRegistered) return;
            _queueDepthRegistered = true;

            _meter.CreateObservableGauge<int>(
                "channel_queue_depth",
                observeValue: () => reader.Count,
                description: "Current number of messages waiting in the local channel queue");
        }
    }
}
