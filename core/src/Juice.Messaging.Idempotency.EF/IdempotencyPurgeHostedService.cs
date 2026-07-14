using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Messaging.Idempotency.EF
{
    /// <summary>
    /// Background service that keeps the EF idempotency store bounded (FR-009) and unblocks stuck keys
    /// (FR-010). On each <see cref="IdempotencyOptions.PurgeInterval"/> tick it:
    /// <list type="bullet">
    /// <item>recovers crashed in-flight records (<see cref="RequestState.InProgress"/>/<see cref="RequestState.New"/>
    /// whose <c>LockedAt</c> is older than <see cref="IdempotencyOptions.InFlightTtl"/>) to
    /// <see cref="RequestState.Failed"/> (retryable), and</item>
    /// <item>deletes records past their <c>ExpiresAt</c>.</item>
    /// </list>
    /// Modeled on the outbox <c>DeliveryHostedService</c> pattern (periodic scoped loop).
    /// </summary>
    public sealed class IdempotencyPurgeHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IdempotencyOptions _options;
        private readonly ILogger<IdempotencyPurgeHostedService> _logger;

        public IdempotencyPurgeHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<IdempotencyPurgeHostedService> logger,
            IOptions<IdempotencyOptions>? options = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options?.Value ?? new IdempotencyOptions();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Idempotency purge loop failed; will retry next interval.");
                }

                try
                {
                    await Task.Delay(_options.PurgeInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>Runs a single recover-then-purge pass. Exposed for testing.</summary>
        public async Task<(int recovered, int purged)> RunOnceAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IdempotencyContext>();

            var now = DateTimeOffset.Now;
            var staleBefore = now - _options.InFlightTtl;

            // Recover crashed in-flight records to a retryable state.
            var recovered = await context.IdempotencyRecords
                .Where(r => (r.State == RequestState.InProgress || r.State == RequestState.New)
                            && r.LockedAt != null && r.LockedAt < staleBefore)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.State, RequestState.Failed)
                    .SetProperty(r => r.LockedAt, (DateTimeOffset?)null),
                    cancellationToken);

            // Purge expired records so the table reaches a bounded steady state.
            var purged = await context.IdempotencyRecords
                .Where(r => r.ExpiresAt != null && r.ExpiresAt < now)
                .ExecuteDeleteAsync(cancellationToken);

            if (recovered > 0 || purged > 0)
            {
                _logger.LogDebug("Idempotency purge: recovered {Recovered} stuck, deleted {Purged} expired.", recovered, purged);
            }

            return (recovered, purged);
        }
    }
}
