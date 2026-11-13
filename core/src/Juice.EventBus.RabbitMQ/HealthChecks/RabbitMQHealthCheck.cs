using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ.HealthChecks
{
    /// <summary>
    /// Checks a connection: connected (reconnecting if it can) and able to open a channel. A failure reports the registration's failure status
    /// (Unhealthy unless it says otherwise), so a host can keep a broker outage from making it unready. It says nothing about consumers:
    /// see <see cref="RabbitMQConsumerHealthCheck"/>.
    /// </summary>
    public class RabbitMQHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _connectionName;

        public RabbitMQHealthCheck(IServiceProvider serviceProvider, string connectionName)
        {
            _serviceProvider = serviceProvider;
            _connectionName = connectionName;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var failure = context.Registration?.FailureStatus ?? HealthStatus.Unhealthy;
            try
            {
                var connection = _serviceProvider.GetKeyedService<IRabbitMQPersistentConnection>(_connectionName);

                if (connection == null)
                {
                    return new HealthCheckResult(failure, $"RabbitMQ connection '{_connectionName}' not registered");
                }

                if (!connection.IsConnected)
                {
                    var reconnected = await connection.TryConnectAsync(cancellationToken);
                    if (!reconnected)
                    {
                        return new HealthCheckResult(failure,
                            $"RabbitMQ connection '{_connectionName}' is not connected and reconnection failed");
                    }
                }

                // Test channel creation
                using var channel = await connection.CreateChannelAsync(cancellationToken);
                if (channel == null)
                {
                    return HealthCheckResult.Degraded(
                        $"RabbitMQ connection '{_connectionName}' is connected but cannot create channel");
                }

                await channel.CloseAsync();

                return HealthCheckResult.Healthy(
                    $"RabbitMQ connection '{_connectionName}' is healthy");
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(failure,
                    $"RabbitMQ connection '{_connectionName}' health check failed: {ex.Message}",
                    ex);
            }
        }
    }
}
