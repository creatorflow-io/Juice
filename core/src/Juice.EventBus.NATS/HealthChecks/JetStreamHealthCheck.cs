using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NATS.Client.Core;

namespace Juice.EventBus.NATS.HealthChecks
{
    internal sealed class JetStreamHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _connectionName;

        public JetStreamHealthCheck(IServiceProvider serviceProvider, string connectionName)
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
                var connection = _serviceProvider.GetKeyedService<IJetStreamConnection>(_connectionName);

                if (connection == null)
                {
                    return HealthCheckResult.Unhealthy(
                        $"JetStream connection '{_connectionName}' not registered");
                }

                if (connection.State == NatsConnectionState.Closed)
                {
                    // connections open lazily; try once so an idle app is not reported unhealthy
                    try
                    {
                        await connection.EnsureConnectedAsync(cancellationToken);
                    }
                    catch (Exception)
                    {
                    }
                }

                var state = connection.State;
                var data = new Dictionary<string, object> { ["state"] = state.ToString() };
                return state switch
                {
                    NatsConnectionState.Open => HealthCheckResult.Healthy(
                        $"JetStream connection '{_connectionName}' is healthy", data),
                    NatsConnectionState.Reconnecting => HealthCheckResult.Degraded(
                        $"JetStream connection '{_connectionName}' is reconnecting", data: data),
                    _ => HealthCheckResult.Unhealthy(
                        $"JetStream connection '{_connectionName}' is {state}", data: data)
                };
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    $"JetStream connection '{_connectionName}' health check failed: {ex.Message}",
                    ex);
            }
        }
    }
}
