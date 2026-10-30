using FluentAssertions;
using Juice.Messaging.Diagnostics;
using Juice.Messaging.Local.Internal;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Juice.OpenTelemetry.Tests
{
    /// <summary>
    /// Outbox delivery and local-channel delivery record the same <c>delivery.metrics</c> instruments, so that both
    /// routes appear as one series per instrument.
    /// </summary>
    public class DeliveryMetricsTests
    {
        private const string Outbox = "delivery-metrics-test-outbox";
        private const string Local = "delivery-metrics-test-local";

        private static readonly string[] SharedInstruments =
            ["delivery_attempt_total", "delivery_success_total", "delivery_failure_total", "delivery_latency_ms"];

        [Fact]
        public void Outbox_and_local_deliveries_should_export_one_stream_per_instrument()
        {
            var metrics = new List<Metric>();
            using var meter = Meter(metrics);

            // the outbox delivery processor records on DeliveryMetrics directly
            DeliveryMetrics.IncrementDeliveryAttempt(Outbox);
            DeliveryMetrics.IncrementDeliverySuccess(Outbox);
            DeliveryMetrics.IncrementDeliveryFailure(Outbox, "TestError");
            DeliveryMetrics.RecordDeliveryLatency(Outbox, TimeSpan.FromMilliseconds(5));

            // the local transports record through LocalChannelMetrics
            LocalChannelMetrics.IncrementDeliveryAttempt(Local);
            LocalChannelMetrics.IncrementDeliverySuccess(Local);
            LocalChannelMetrics.IncrementDeliveryFailure(Local, "TestError");
            LocalChannelMetrics.RecordDeliveryLatency(Local, TimeSpan.FromMilliseconds(5));
            LocalChannelMetrics.IncrementChannelDropped(Local, "TestMessage");

            meter.ForceFlush();
            foreach (var name in SharedInstruments)
            {
                metrics.Where(m => m.Name == name).Should().ContainSingle("{0} is one instrument for both routes", name);
            }
            foreach (var publisher in new[] { Outbox, Local })
            {
                MetricValues.Sum(metrics, "delivery_attempt_total", "publisher", publisher).Should().Be(1);
                MetricValues.Sum(metrics, "delivery_success_total", "publisher", publisher).Should().Be(1);
                MetricValues.Sum(metrics, "delivery_failure_total", "publisher", publisher).Should().Be(1);
                MetricValues.Count(metrics, "delivery_latency_ms", "publisher", publisher).Should().Be(1);
            }
            metrics.Single(m => m.Name == "delivery_latency_ms").Unit.Should().Be("ms");
            MetricValues.Sum(metrics, "channel_dropped_total", "publisher", Local).Should().Be(1, "local-only instruments stay on delivery.metrics");
        }

        private static MeterProvider Meter(ICollection<Metric> metrics)
            => Sdk.CreateMeterProviderBuilder()
                .AddMeter(DeliveryMetrics.MeterName)
                .AddInMemoryExporter(metrics)
                .Build();
    }
}
