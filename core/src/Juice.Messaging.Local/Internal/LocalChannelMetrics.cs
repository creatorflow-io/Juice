namespace Juice.Messaging.Local.Internal
{
    using System.Diagnostics.Metrics;
    using System.Threading.Channels;
    using Juice.Messaging.Diagnostics;

    /// <summary>
    /// Delivery metrics for the local-channel and local transport publisher paths.
    /// The shared delivery instruments are <see cref="DeliveryMetrics"/>'s, so local and broker routes appear in one
    /// series; the local-only instruments below are on the same <c>delivery.metrics</c> meter.
    /// </summary>
    internal static class LocalChannelMetrics
    {
        private static readonly Meter _meter = DeliveryMetrics.Meter;

        private static readonly Counter<long> ChannelDroppedCounter =
            _meter.CreateCounter<long>(
                "channel_dropped_total",
                description: "Messages dropped because the local channel was full");

        private static readonly Counter<long> Phase1FallbackCounter =
            _meter.CreateCounter<long>(
                "local_delivery_phase1_fallback_total",
                description: "Deliveries for the 'local' route that reached the background delivery processor despite being dispatched in phase 1 — indicates phase 1 did not mark the outbox record Published (e.g. DB unavailable during marking)");

        // Registered lazily when the channel is created so the gauge holds a live reference.
        private static bool _queueDepthRegistered;

        public static void IncrementDeliveryAttempt(string publisherKey)
            => DeliveryMetrics.IncrementDeliveryAttempt(publisherKey);

        public static void IncrementDeliverySuccess(string publisherKey)
            => DeliveryMetrics.IncrementDeliverySuccess(publisherKey);

        public static void IncrementDeliveryFailure(string publisherKey, string errorType)
            => DeliveryMetrics.IncrementDeliveryFailure(publisherKey, errorType);

        public static void RecordDeliveryLatency(string publisherKey, TimeSpan elapsed)
            => DeliveryMetrics.RecordDeliveryLatency(publisherKey, elapsed);

        public static void IncrementPhase1Fallback(string publisherKey)
        {
            Phase1FallbackCounter.Add(
                1,
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
