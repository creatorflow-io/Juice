using System.Diagnostics.Metrics;

namespace Juice.Messaging.Diagnostics
{
    /// <summary>
    /// The one <see cref="Meter"/> Juice records its framework metrics on (<c>juice.*</c> instruments).
    /// <para>The host collects them by adding <see cref="Name"/> to its meter provider (<c>AddMeter(JuiceMeter.Name)</c>).
    /// Instruments are no-ops while nothing listens.</para>
    /// <para>The older meters (<c>delivery.metrics</c>, <c>infra.rabbitmq.metrics</c>, <c>infra.nats.metrics</c>) are kept
    /// unchanged for existing dashboards.</para>
    /// </summary>
    public static class JuiceMeter
    {
        public const string Name = JuiceActivitySource.Name;

        public static readonly Meter Meter = new(Name, JuiceActivitySource.Source.Version);
    }
}
