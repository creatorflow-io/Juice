using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Outbox.Delivery.Processing
{
    /// <summary>
    /// Basic background service that polls outbox deliveries on a fixed interval with exponential backoff.
    /// Delegates all batch logic to an injected <see cref="DeliveryWorker{TContext}"/>.
    /// </summary>
    internal sealed class DeliveryHostedService<TContext> : BackgroundService
    {
        private readonly DeliveryWorker<TContext> _worker;
        private readonly ILogger<DeliveryHostedService<TContext>> _logger;

        public string Publisher => _worker.Publisher;
        public string Intent => _worker.Intent;

        public DeliveryHostedService(
            DeliveryWorker<TContext> worker,
            ILogger<DeliveryHostedService<TContext>> logger)
        {
            _worker = worker;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _worker.InitializeAsync(stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Service started for Publisher: {Publisher}, Intent: {Intent}, policy: {Policy}",
                    Publisher, Intent, _worker.Policy);
            }

            var consecutiveFailures = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _worker.ExecuteAsync(stoppingToken);
                    consecutiveFailures = 0;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Service stopping for Publisher: {Publisher}, Intent: {Intent}",
                        Publisher, Intent);
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveFailures++;
                    _logger.LogError("Error processing deliveries for Publisher: {Publisher}, Intent: {Intent}. {Message}",
                        Publisher, Intent, ex.Message);
                    await Task.Delay(_worker.Policy.GetBackoffDelay(consecutiveFailures), stoppingToken);
                    continue;
                }

                await Task.Delay(_worker.Policy.Interval, stoppingToken);
            }
        }
    }
}
