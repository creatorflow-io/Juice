using Juice.EventBus.RabbitMQ.Consuming;
using Juice.EventBus.RabbitMQ.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RabbitMQHealthCheckExtensions
    {
        /// <summary>
        /// Checks a connection: connected (reconnecting if it can) and able to open a channel. A failure reports
        /// <paramref name="failureStatus"/> (Unhealthy by default).
        /// </summary>
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

        /// <summary>
        /// Checks that the consumer added with <paramref name="consumerKey"/> is registered with the broker (<see cref="RabbitMQConsumerStatus"/>):
        /// not while it is starting, recovering or stopped. It reads the consumer's state and never touches the broker. A failure reports
        /// <paramref name="failureStatus"/> (Unhealthy by default).
        /// </summary>
        public static IHealthChecksBuilder AddRabbitMQConsumerHealthCheck(
            this IHealthChecksBuilder builder,
            string consumerKey,
            string? name = null,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
        {
            return builder.Add(new HealthCheckRegistration(
                name ?? $"rabbitmq_consumer_{consumerKey}",
                sp => new RabbitMQConsumerHealthCheck(sp.GetRequiredKeyedService<RabbitMQConsumerStatus>(consumerKey)),
                failureStatus,
                tags ?? new[] { "rabbitmq", "messaging" },
                timeout));
        }
    }
}
