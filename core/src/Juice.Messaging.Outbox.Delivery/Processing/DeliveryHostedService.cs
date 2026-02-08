using Juice.EventBus.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Outbox.Delivery.Processing
{
    /// <summary>
    /// Background service that processes outbox deliveries for a specific Publisher × Intent combination.
    /// Creates a new scope for each batch to ensure proper DbContext lifecycle management.
    /// </summary>
    internal sealed class DeliveryHostedService<TContext> : BackgroundService
    {
        private readonly IServiceProvider _scopeFactory;
        private readonly IDeliveryPolicyResolver _policyProvider;
        private readonly string _publisher;
        private readonly string _intent;
        private readonly ILogger<DeliveryHostedService<TContext>> _logger;

        public string Publisher => _publisher;
        public string Intent => _intent;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeliveryHostedService{TContext}"/> class.
        /// </summary>
        /// <param name="scopeFactory">Factory to create service scopes for each processing batch.</param>
        /// <param name="publisher">The event publisher for this delivery service.</param>
        /// <param name="intent">The outbox intent (e.g., SendPending, RetryFailed, RecoverTimeout).</param>
        public DeliveryHostedService(
            IServiceProvider scopeFactory,
            string publisher,
            string intent)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _intent = intent ?? throw new ArgumentNullException(nameof(intent));
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _policyProvider = scopeFactory.GetRequiredService<IDeliveryPolicyResolver>();
            _logger = scopeFactory.GetRequiredService<ILogger<DeliveryHostedService<TContext>>>();
        }

        /// <summary>
        /// Executes the delivery processing loop until cancellation is requested.
        /// </summary>
        /// <param name="stoppingToken">Triggered when the host is performing a graceful shutdown.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // ResolveAsync policy once since it's typically static per Publisher × Intent combination
            var policy = await _policyProvider.GetPolicyAsync(new DeliveryContext(_publisher, _intent, typeof(TContext).Name), stoppingToken);
            var consecutiveFailures = 0;
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Service started for Publisher: {Publisher}, Intent: {Intent}, policy: {Policy}", Publisher, Intent, policy);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(policy, stoppingToken);
                    consecutiveFailures = 0; // Reset on success
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown - exit the loop
                    _logger.LogInformation("Service stopping for Publisher: {Publisher}, Intent: {Intent}", Publisher, Intent);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error processing deliveries for Publisher: {Publisher}, Intent: {Intent}. {Message}", Publisher, Intent, ex.Message);
                    consecutiveFailures++;
                    // Exponential backoff: 5s, 10s, 20s, 40s... capped at 5 minutes
                    var backoffDelay = policy.GetBackoffDelay(consecutiveFailures);

                    await Task.Delay(backoffDelay, stoppingToken);
                    continue; // Skip normal interval delay
                }

                await Task.Delay(policy.Interval, stoppingToken);
            }
        }

        /// <summary>
        /// Processes a single batch of deliveries within a scoped service context.
        /// </summary>
        /// <param name="policy">The delivery policy to apply.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ProcessBatchAsync(DeliveryPolicy policy, CancellationToken cancellationToken)
        {
            // Create a new scope for each batch to ensure:
            // 1. Fresh DbContext instance (avoids stale data)
            // 2. Proper disposal of scoped services
            // 3. Isolation between batches
            using var scope = _scopeFactory.CreateScope();
            var intent = scope.ServiceProvider.GetRequiredKeyedService<IDeliveryIntent<TContext>>(_intent);
            var publisher = scope.ServiceProvider.GetRequiredKeyedService<ITransportPublisher>(_publisher);
            var processor = scope.ServiceProvider.GetRequiredService<DeliveryProcessor<TContext>>();
            processor.Configure(publisher, policy);

            var deliveries = await intent.RetrieveDeliveriesAsync(_publisher, policy, cancellationToken);
            _logger.LogInformation("Retrieved {Count} deliveries for Publisher: {Publisher}, Intent: {Intent}", deliveries.Count(), _publisher, _intent);
            if (deliveries.Any())
            {
                await processor.ProcessAsync([.. deliveries], cancellationToken);
            }
        }
    }
}
