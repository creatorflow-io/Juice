using System.Diagnostics.Metrics;

namespace Juice.Messaging.Diagnostics
{
    /// <summary>
    /// The <c>delivery.metrics</c> meter: delivery attempts, outcomes and latency for outbox delivery and the local
    /// transports.
    /// <para>Every route records these same instruments, defined only here, so they export as one series per instrument,
    /// told apart by the <c>publisher</c> dimension. The host collects them with <c>AddMeter(DeliveryMetrics.MeterName)</c>;
    /// <c>AddJuiceInstrumentation()</c> does not. Names and units are kept unchanged for existing dashboards; new framework
    /// metrics go on <see cref="JuiceMeter"/>.</para>
    /// </summary>
    public static class DeliveryMetrics
    {
        public const string MeterName = "delivery.metrics";

        /// <summary>
        /// Also carries transport-specific delivery instruments (for example the local channel's queue depth).
        /// </summary>
        public static readonly Meter Meter = new(MeterName, "1.0.0");

        private static readonly Counter<long> _attempts = Meter.CreateCounter<long>(
            "delivery_attempt_total", description: "Total delivery attempts");

        private static readonly Counter<long> _successes = Meter.CreateCounter<long>(
            "delivery_success_total", description: "Total successful deliveries");

        private static readonly Counter<long> _failures = Meter.CreateCounter<long>(
            "delivery_failure_total", description: "Total failed deliveries");

        private static readonly Histogram<double> _latency = Meter.CreateHistogram<double>(
            "delivery_latency_ms", unit: "ms", description: "Delivery latency in milliseconds");

        public static void IncrementDeliveryAttempt(string publisherKey)
            => _attempts.Add(1, new KeyValuePair<string, object?>("publisher", publisherKey));

        public static void IncrementDeliverySuccess(string publisherKey)
            => _successes.Add(1, new KeyValuePair<string, object?>("publisher", publisherKey));

        public static void IncrementDeliveryFailure(string publisherKey, string errorType)
            => _failures.Add(1,
                new KeyValuePair<string, object?>("publisher", publisherKey),
                new KeyValuePair<string, object?>("error_type", errorType));

        public static void RecordDeliveryLatency(string publisherKey, TimeSpan elapsed)
            => _latency.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("publisher", publisherKey));
    }
}
