using FluentAssertions;
using Juice.EventBus.NATS;
using Juice.EventBus.RabbitMQ;
using Juice.MediatR;
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

        [Fact]
        public void AddJuiceDeliveryMetrics_should_collect_every_meter_outside_the_juice_meter()
        {
            var metrics = new List<Metric>();
            using var meter = Sdk.CreateMeterProviderBuilder().AddJuiceDeliveryMetrics().AddInMemoryExporter(metrics).Build();

            DeliveryMetrics.IncrementDeliveryAttempt(Outbox);
            MediatorMetrics.IncrementIdentifiedCommandReceived("DeliveryMetricsTestCommand");
            RabbitMQMetrics.IncrementChannelError("delivery-metrics-test");
            JetStreamMetrics.IncrementParked("delivery-metrics-test");

            meter.ForceFlush();
            metrics.Select(m => m.MeterName).Distinct()
                .Should().BeEquivalentTo(DeliveryMetrics.MeterName, "infra.rabbitmq.metrics", "infra.nats.metrics");
            metrics.Select(m => m.Name).Should().Contain(new[]
            {
                "delivery_attempt_total", "identified_commands_received", "rabbitmq_channel_error_total", "jetstream_consume_parked_total"
            });
        }

        [Fact]
        public void JetStream_publish_errors_should_be_tagged_by_destination_not_subject()
        {
            var metrics = new List<Metric>();
            using var meter = Sdk.CreateMeterProviderBuilder().AddJuiceDeliveryMetrics().AddInMemoryExporter(metrics).Build();

            JetStreamMetrics.IncrementPublishError("delivery-metrics-test");

            meter.ForceFlush();
            MetricValues.Sum(metrics, "jetstream_publish_error_total", "destination", "delivery-metrics-test").Should().Be(1);
            var keys = new List<string>();
            foreach (ref readonly var point in metrics.Single(m => m.Name == "jetstream_publish_error_total").GetMetricPoints())
            {
                foreach (var tag in point.Tags) keys.Add(tag.Key);
            }
            keys.Should().NotContain("subject", "a subject includes the routing key, which can carry unbounded values");
        }

        [Fact]
        public void AddJuiceInstrumentation_should_not_collect_the_delivery_meters()
        {
            var metrics = new List<Metric>();
            using var meter = Sdk.CreateMeterProviderBuilder().AddJuiceInstrumentation().AddInMemoryExporter(metrics).Build();

            DeliveryMetrics.IncrementDeliveryAttempt(Outbox);
            RabbitMQMetrics.IncrementChannelError("delivery-metrics-test");

            meter.ForceFlush();
            metrics.Should().NotContain(m => m.MeterName == DeliveryMetrics.MeterName || m.MeterName == "infra.rabbitmq.metrics");
        }

        private static MeterProvider Meter(ICollection<Metric> metrics)
            => Sdk.CreateMeterProviderBuilder()
                .AddMeter(DeliveryMetrics.MeterName)
                .AddInMemoryExporter(metrics)
                .Build();
    }
}
