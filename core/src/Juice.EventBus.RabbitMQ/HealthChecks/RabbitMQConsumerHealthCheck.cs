using Juice.EventBus.RabbitMQ.Consuming;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Juice.EventBus.RabbitMQ.HealthChecks
{
    /// <summary>
    /// Healthy while the consumer is registered with the broker; otherwise the registration's failure status, with why. It only reads the
    /// consumer's state (<see cref="RabbitMQConsumerStatus"/>): it never connects or opens a channel.
    /// </summary>
    public sealed class RabbitMQConsumerHealthCheck : IHealthCheck
    {
        private readonly RabbitMQConsumerStatus _status;

        public RabbitMQConsumerHealthCheck(RabbitMQConsumerStatus status)
        {
            _status = status;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var (state, reason, since) = _status.Snapshot();
            var data = new Dictionary<string, object>
            {
                ["queue"] = _status.Queue,
                ["state"] = state.ToString(),
                ["since"] = since,
            };
            if (state == RabbitMQConsumerState.Consuming)
            {
                return Task.FromResult(HealthCheckResult.Healthy($"Consuming from '{_status.Queue}'", data));
            }
            var description = $"Not consuming from '{_status.Queue}' ({state} since {since:O})" + (reason is null ? "" : $": {reason}");
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, description, data: data));
        }
    }
}
