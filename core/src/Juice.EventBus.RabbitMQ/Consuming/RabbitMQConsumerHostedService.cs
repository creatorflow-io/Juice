using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.RabbitMQ.Consuming
{
    internal sealed class RabbitMQConsumerHostedService : BackgroundService
    {
        private readonly RabbitMQConsumerEngine _engine;
        private readonly RabbitMQConsumerEndpoint _endpoint;
        private readonly ILogger<RabbitMQConsumerHostedService> _logger;

        public RabbitMQConsumerHostedService(
            RabbitMQConsumerEngine engine,
            RabbitMQConsumerEndpoint endpoint,
            ILogger<RabbitMQConsumerHostedService> logger)
        {
            _engine = engine;
            _endpoint = endpoint;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[RabbitMQ:{Queue}] HostedService starting",
                _endpoint.Queue);

            stoppingToken.Register(async () =>
            {
                _logger.LogInformation(
                    "[RabbitMQ:{Queue}] HostedService stopping",
                    _endpoint.Queue);

               await _engine.StopAsync();
            });

            var ok = await _engine.StartAsync(_endpoint, stoppingToken);
            if (!ok) {
                _logger.LogError(
                    "[RabbitMQ:{Queue}] HostedService failed to start the consumer engine",
                    _endpoint.Queue);
            }
        }
    }
}
