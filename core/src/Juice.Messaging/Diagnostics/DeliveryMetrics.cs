using System.Diagnostics.Metrics;

namespace Juice.Messaging.Diagnostics
{
    /// <summary>
    /// Delivery attempts, outcomes and latency for outbox delivery and the local transports, recorded twice:
    /// <list type="bullet">
    /// <item>on <see cref="JuiceMeter"/> as <c>juice.delivery.attempts</c>, <c>juice.delivery.successes</c>,
    /// <c>juice.delivery.failures</c> and <c>juice.delivery.duration</c> (seconds), told apart by <c>juice.publisher</c>
    /// and, for failures, <c>error.type</c>. <c>AddJuiceInstrumentation()</c> collects them.</item>
    /// <item>on the <c>delivery.metrics</c> meter under their original names (<c>delivery_attempt_total</c>,
    /// <c>delivery_success_total</c>, <c>delivery_failure_total</c>, <c>delivery_latency_ms</c>), told apart by
    /// <c>publisher</c>, kept unchanged for existing dashboards. The host collects them with
    /// <c>AddJuiceDeliveryMetrics()</c>.</item>
    /// </list>
    /// <para>Every route records these same instruments, defined only here, so they export as one series per instrument.
    /// The unprefixed names can clash with another library's: read the <c>juice.delivery.*</c> ones.</para>
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

        private static readonly Counter<long> _juiceAttempts = JuiceMeter.Meter.CreateCounter<long>(
            "juice.delivery.attempts", unit: "{attempt}", description: "Delivery attempts, by publisher.");

        private static readonly Counter<long> _juiceSuccesses = JuiceMeter.Meter.CreateCounter<long>(
            "juice.delivery.successes", unit: "{delivery}", description: "Deliveries the publisher accepted.");

        private static readonly Counter<long> _juiceFailures = JuiceMeter.Meter.CreateCounter<long>(
            "juice.delivery.failures", unit: "{failure}", description: "Deliveries that failed or were abandoned, by publisher and error type.");

        private static readonly Histogram<double> _juiceDuration = JuiceMeter.Meter.CreateHistogram<double>(
            "juice.delivery.duration", unit: "s", description: "Duration of one delivery attempt.");

        private static KeyValuePair<string, object?> Publisher(string publisherKey)
            => new(MessagingActivity.Tags.Publisher, publisherKey);

        public static void IncrementDeliveryAttempt(string publisherKey)
        {
            _attempts.Add(1, new KeyValuePair<string, object?>("publisher", publisherKey));
            _juiceAttempts.Add(1, Publisher(publisherKey));
        }

        public static void IncrementDeliverySuccess(string publisherKey)
        {
            _successes.Add(1, new KeyValuePair<string, object?>("publisher", publisherKey));
            _juiceSuccesses.Add(1, Publisher(publisherKey));
        }

        public static void IncrementDeliveryFailure(string publisherKey, string errorType)
        {
            _failures.Add(1,
                new KeyValuePair<string, object?>("publisher", publisherKey),
                new KeyValuePair<string, object?>("error_type", errorType));
            _juiceFailures.Add(1, Publisher(publisherKey), new KeyValuePair<string, object?>("error.type", errorType));
        }

        public static void RecordDeliveryLatency(string publisherKey, TimeSpan elapsed)
        {
            _latency.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("publisher", publisherKey));
            _juiceDuration.Record(elapsed.TotalSeconds, Publisher(publisherKey));
        }
    }
}
