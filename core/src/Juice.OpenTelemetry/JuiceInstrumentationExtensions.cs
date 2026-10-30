using Juice.Messaging.Diagnostics;

namespace OpenTelemetry.Trace
{
    public static class JuiceTracerProviderBuilderExtensions
    {
        /// <summary>
        /// Collects the Juice spans (<see cref="JuiceActivitySource.Name"/>): messaging, outbox and, with
        /// <c>services.AddJuiceOpenTelemetry()</c>, the mediator.
        /// </summary>
        public static TracerProviderBuilder AddJuiceInstrumentation(this TracerProviderBuilder builder)
            => builder.AddSource(JuiceActivitySource.Name);
    }
}

namespace OpenTelemetry.Metrics
{
    public static class JuiceMeterProviderBuilderExtensions
    {
        // Declared by Juice.EventBus.RabbitMQ (RabbitMQMetrics) and Juice.EventBus.NATS (JetStreamMetrics), which this
        // package does not reference; DeliveryMetricsTests fails if a name drifts.
        private const string RabbitMQMeterName = "infra.rabbitmq.metrics";
        private const string NatsMeterName = "infra.nats.metrics";

        /// <summary>
        /// Collects the <c>juice.*</c> metrics (<see cref="JuiceMeter.Name"/>). It does not include the delivery and
        /// broker meters; add <see cref="AddJuiceDeliveryMetrics"/> for those.
        /// </summary>
        public static MeterProviderBuilder AddJuiceInstrumentation(this MeterProviderBuilder builder)
            => builder.AddMeter(JuiceMeter.Name);

        /// <summary>
        /// Collects the meters Juice records beside <see cref="JuiceMeter.Name"/>, kept for existing dashboards:
        /// <list type="bullet">
        /// <item><c>delivery.metrics</c> (<see cref="DeliveryMetrics.MeterName"/>): delivery attempts, outcomes and
        /// latency for outbox and local delivery, the local channel's drops and queue depth, and idempotent-command
        /// counts.</item>
        /// <item><c>infra.rabbitmq.metrics</c>: RabbitMQ channel errors.</item>
        /// <item><c>infra.nats.metrics</c>: JetStream publish errors, consume retries and parked messages.</item>
        /// </list>
        /// Use it together with <see cref="AddJuiceInstrumentation"/>. A meter whose package is not installed records
        /// nothing.
        /// </summary>
        public static MeterProviderBuilder AddJuiceDeliveryMetrics(this MeterProviderBuilder builder)
            => builder.AddMeter(DeliveryMetrics.MeterName, RabbitMQMeterName, NatsMeterName);
    }
}
