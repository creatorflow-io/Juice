using Juice.EventBus.Subscriptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.RabbitMQ.Consuming
{
    /// <summary>
    /// Runs one consumer. Its start is retried until it consumes or the host stops (the broker or the queue may not be there yet), so the
    /// host's start isn't held up and a consumer is never left stopped. On shutdown it stops taking messages and waits, within the host's
    /// shutdown timeout, for those being handled.
    /// </summary>
    internal sealed class RabbitMQConsumerHostedService : BackgroundService
    {
        private readonly string _serviceKey;
        private readonly RabbitMQConsumerEngine _engine;
        private readonly RabbitMQConsumerEndpoint _endpoint;
        private readonly ILogger<RabbitMQConsumerHostedService> _logger;
        private readonly ISubscriptionsManager _subscriptionsManager;
        private readonly RabbitMQConsumerStatus _status;

        public RabbitMQConsumerHostedService(
            string serviceKey,
            RabbitMQConsumerEngine engine,
            RabbitMQConsumerEndpoint endpoint,
            ISubscriptionsManager subscriptionsManager,
            RabbitMQConsumerStatus status,
            ILogger<RabbitMQConsumerHostedService> logger)
        {
            _serviceKey = serviceKey;
            _engine = engine;
            _endpoint = endpoint;
            _logger = logger;
            _subscriptionsManager = subscriptionsManager;
            _status = status;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[RabbitMQ:{Queue}] HostedService starting",
                _endpoint.Queue);

            await _engine.RunAsync(_serviceKey, _endpoint, _subscriptionsManager, _status, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[RabbitMQ:{Queue}] HostedService stopping",
                _endpoint.Queue);
            try
            {
                await base.StopAsync(cancellationToken);
            }
            finally
            {
                await _engine.StopAsync(cancellationToken);
            }
        }
    }
}
