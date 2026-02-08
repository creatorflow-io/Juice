using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Juice.Messaging.Outbox.EF.HealthChecks
{
    public class OutboxDeliveryHealthCheck<TContext> : IHealthCheck
        where TContext : DbContext, IOutboxContext
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly OutboxHealthCheckOptions _options;

        public OutboxDeliveryHealthCheck(
            IServiceScopeFactory scopeFactory,
            OutboxHealthCheckOptions options)
        {
            _scopeFactory = scopeFactory;
            _options = options;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outboxRepo = scope.ServiceProvider.GetService<IOutboxRepository<TContext>>();

                if (outboxRepo == null)
                {
                    return HealthCheckResult.Degraded("Outbox repository not configured");
                }

                var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

                // Check for stuck messages
                var threshold = DateTimeOffset.UtcNow.AddMinutes(-_options.StuckMessageThresholdMinutes);
                var stuckCount = await dbContext.OutboxDeliveries.AsNoTracking()
                    .Where(d => d.State == DeliveryState.InProgress && d.ProcessedOn < threshold)
                    .CountAsync(cancellationToken);

                // Check for failed messages
                var failedCount = await dbContext.OutboxDeliveries.AsNoTracking()
                    .Where(d => d.State == DeliveryState.Failed
                        && d.RetryCount >= _options.MaxFailedRetries)
                    .CountAsync(cancellationToken);

                var data = new Dictionary<string, object>
                {
                    { "stuck_messages", stuckCount },
                    { "permanently_failed", failedCount }
                };

                if (stuckCount > _options.MaxStuckMessages)
                {
                    return HealthCheckResult.Degraded(
                        $"{stuckCount} messages stuck in processing",
                        data: data);
                }

                if (failedCount > _options.MaxPermanentFailures)
                {
                    return HealthCheckResult.Unhealthy(
                        $"{failedCount} messages permanently failed",
                        data: data);
                }

                return HealthCheckResult.Healthy("Outbox delivery is healthy", data: data);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    "Outbox delivery health check failed",
                    ex);
            }
        }
    }

    public class OutboxHealthCheckOptions
    {
        public int StuckMessageThresholdMinutes { get; set; } = 10;
        public int MaxStuckMessages { get; set; } = 100;
        public int MaxPermanentFailures { get; set; } = 50;
        public int MaxFailedRetries { get; set; } = 5;
    }
}
