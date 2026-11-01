using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using FluentAssertions;
using Juice.EventBus.NATS;
using Juice.EventBus.RabbitMQ;
using Juice.MediatR;
using Juice.Messaging.Diagnostics;
using Juice.Messaging.Local.Internal;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Juice.OpenTelemetry.Tests
{
    /// <summary>
    /// G1 acceptance: a host that registers every Juice meter (<c>Juice</c>, <c>delivery.metrics</c>,
    /// <c>infra.rabbitmq.metrics</c>, <c>infra.nats.metrics</c>) exports a measurement from each, and the SDK reports no
    /// duplicate instrument for them.
    /// </summary>
    public class MeterRegistrationTests
    {
        private static readonly string[] JuiceMeters =
            [JuiceMeter.Name, DeliveryMetrics.MeterName, "infra.rabbitmq.metrics", "infra.nats.metrics"];

        [Fact]
        public async Task Host_should_export_every_juice_meter_without_duplicate_instruments_Async()
        {
            using var duplicates = new DuplicateInstrumentListener();
            var metrics = new List<Metric>();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(b => b.RegisterServicesFromAssemblyContaining<MeterRegistrationTests>(includeNonPublicTypes: false));
            services.AddJuiceOpenTelemetry();
            services.AddOpenTelemetry().WithMetrics(meters => meters
                .AddJuiceInstrumentation()
                .AddJuiceDeliveryMetrics()
                .AddInMemoryExporter(metrics));
            using var provider = services.BuildServiceProvider();
            var meterProvider = provider.GetRequiredService<MeterProvider>();

            // Juice meter: through the mediator, as a host records it
            (await provider.GetRequiredService<IMediator>().Send(new PingRequest("meters"))).Should().Be("pong:meters");
            // delivery.metrics from both classes that record on it (outbox and local channel share instruments)
            DeliveryMetrics.IncrementDeliveryAttempt("meter-registration-outbox");
            LocalChannelMetrics.IncrementDeliveryAttempt("meter-registration-local");
            MediatorMetrics.IncrementIdentifiedCommandReceived("MeterRegistrationCommand");
            // broker meters record only on errors, retries and parking; recorded here through the same internal calls
            RabbitMQMetrics.IncrementChannelError("meter-registration");
            JetStreamMetrics.IncrementRetry("meter-registration");

            meterProvider.ForceFlush();
            duplicates.For(JuiceMeters).Should().BeEmpty("an instrument defined twice with different unit or description drifts");
            metrics.Select(m => m.MeterName).Distinct().Should().Contain(JuiceMeters);
            metrics.Select(m => m.Name).Should().Contain(new[]
            {
                "juice.mediator.requests", "delivery_attempt_total", "identified_commands_received",
                "rabbitmq_channel_error_total", "jetstream_consume_retry_total"
            });
            metrics.Where(m => m.Name == "delivery_attempt_total").Should().ContainSingle();
        }

        [Fact]
        public void Listener_should_report_a_duplicate_instrument()
        {
            // Guards the check above: two instruments with one name and different units on one meter.
            const string meterName = "Juice.OpenTelemetry.Tests.Duplicate";
            using var duplicates = new DuplicateInstrumentListener();
            using var meter = new Meter(meterName);
            meter.CreateCounter<long>("conflict", unit: "ms");
            meter.CreateCounter<long>("conflict", unit: "s");

            using var provider = global::OpenTelemetry.Sdk.CreateMeterProviderBuilder().AddMeter(meterName)
                .AddInMemoryExporter(new List<Metric>()).Build();

            duplicates.For([meterName]).Should().NotBeEmpty();
        }

        /// <summary>
        /// Collects the SDK's duplicate-instrument warnings (OpenTelemetry-Sdk EventSource), with the meter they name.
        /// </summary>
        private sealed class DuplicateInstrumentListener : EventListener
        {
            private readonly ConcurrentQueue<string> _events = new();

            public IEnumerable<string> For(IEnumerable<string> meterNames)
                => _events.Where(e => meterNames.Any(m => e.Contains($"'{m}'", StringComparison.Ordinal)
                    || e.Contains($" {m} ", StringComparison.Ordinal) || e.Contains($"={m};", StringComparison.Ordinal)));

            protected override void OnEventSourceCreated(EventSource source)
            {
                if (source.Name == "OpenTelemetry-Sdk")
                {
                    EnableEvents(source, EventLevel.Warning);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs e)
            {
                if (e.EventName?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // name=value pairs, so a meter name can be matched exactly
                    var payload = string.Join(";", (e.PayloadNames ?? (IEnumerable<string>)[]).Zip(e.Payload ?? (IEnumerable<object?>)[], (n, v) => $"{n}={v}"));
                    _events.Enqueue($"{e.EventName} {payload};");
                }
            }
        }
    }
}
