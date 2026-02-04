using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ.HealthChecks
{
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
            try
            {
                var connection = _serviceProvider.GetKeyedService<IRabbitMQPersistentConnection>(_connectionName);

                if (connection == null)
                {
                    return HealthCheckResult.Unhealthy(
                        $"RabbitMQ connection '{_connectionName}' not registered");
                }

                if (!connection.IsConnected)
                {
                    var reconnected = await connection.TryConnectAsync(cancellationToken);
                    if (!reconnected)
                    {
                        return HealthCheckResult.Unhealthy(
                            $"RabbitMQ connection '{_connectionName}' is not connected and reconnection failed");
                    }
                }

                // Test channel creation
                var channel = await connection.CreateChannelAsync(cancellationToken);
                if (channel == null)
                {
                    return HealthCheckResult.Degraded(
                        $"RabbitMQ connection '{_connectionName}' is connected but cannot create channel");
                }

                await channel.CloseAsync();
                channel.Dispose();

                return HealthCheckResult.Healthy(
                    $"RabbitMQ connection '{_connectionName}' is healthy");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    $"RabbitMQ connection '{_connectionName}' health check failed: {ex.Message}",
                    ex);
            }
        }
    }
}
