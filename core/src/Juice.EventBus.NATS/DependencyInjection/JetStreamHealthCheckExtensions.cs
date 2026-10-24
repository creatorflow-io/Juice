using Juice.EventBus.NATS.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class JetStreamHealthCheckExtensions
    {
        public static IHealthChecksBuilder AddJetStreamHealthCheck(
            this IHealthChecksBuilder builder,
            string connectionName,
            string? name = null,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
        {
            return builder.Add(new HealthCheckRegistration(
                name ?? $"nats_{connectionName}",
                sp => new JetStreamHealthCheck(sp, connectionName),
                failureStatus,
                tags ?? new[] { "nats", "messaging" },
                timeout));
        }
    }
}
