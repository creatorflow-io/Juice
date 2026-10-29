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
        /// <summary>
        /// Collects the <c>juice.*</c> metrics (<see cref="JuiceMeter.Name"/>).
        /// </summary>
        public static MeterProviderBuilder AddJuiceInstrumentation(this MeterProviderBuilder builder)
            => builder.AddMeter(JuiceMeter.Name);
    }
}
