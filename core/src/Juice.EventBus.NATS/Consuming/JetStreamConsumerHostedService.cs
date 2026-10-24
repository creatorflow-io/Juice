using Juice.EventBus.Subscriptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.NATS.Consuming
{
    internal sealed class JetStreamConsumerHostedService : BackgroundService
    {
        private readonly string _serviceKey;
        private readonly JetStreamConsumerEngine _engine;
        private readonly JetStreamConsumerEndpoint _endpoint;
        private readonly ILogger<JetStreamConsumerHostedService> _logger;
        private readonly ISubscriptionsManager _subscriptionsManager;

        public JetStreamConsumerHostedService(
            string serviceKey,
            JetStreamConsumerEngine engine,
            JetStreamConsumerEndpoint endpoint,
            ISubscriptionsManager subscriptionsManager,
            ILogger<JetStreamConsumerHostedService> logger)
        {
            _serviceKey = serviceKey;
            _engine = engine;
            _endpoint = endpoint;
            _logger = logger;
            _subscriptionsManager = subscriptionsManager;
        }

        internal JetStreamConsumerEndpoint Endpoint => _endpoint;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[JetStream:{Stream}/{Durable}] HostedService starting",
                _endpoint.Stream, _endpoint.DurableName);
            try
            {
                await _engine.StartAsync(_serviceKey, _endpoint, _subscriptionsManager, stoppingToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex,
                    "[JetStream:{Stream}/{Durable}] HostedService failed to start the consumer engine. {Message}",
                    _endpoint.Stream, _endpoint.DurableName, ex.Message);
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[JetStream:{Stream}/{Durable}] HostedService stopping",
                _endpoint.Stream, _endpoint.DurableName);

            await _engine.StopAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
