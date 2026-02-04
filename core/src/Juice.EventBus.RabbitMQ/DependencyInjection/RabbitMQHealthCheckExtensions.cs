using Juice.EventBus.RabbitMQ.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RabbitMQHealthCheckExtensions
    {
        public static IHealthChecksBuilder AddRabbitMQHealthCheck(
            this IHealthChecksBuilder builder,
            string connectionName,
            string? name = null,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
        {
            return builder.Add(new HealthCheckRegistration(
                name ?? $"rabbitmq_{connectionName}",
                sp => new RabbitMQHealthCheck(sp, connectionName),
                failureStatus,
                tags ?? new[] { "rabbitmq", "messaging" },
                timeout));
        }
    }
}
