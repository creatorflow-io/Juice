using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Delivery.Processing
{
    internal sealed class CompositeDeliveryHostedService<TContext> : IHostedService, IDisposable
    {
        private readonly DeliveryHostedService<TContext>[] _hostedServices;
        private readonly ILogger _logger;
        public string Publisher { get; init; }

        public CompositeDeliveryHostedService(
            string publisher,
            IEnumerable<string> intents,
            IServiceProvider serviceProvider
            )
        {
            Publisher = publisher;
            _logger = serviceProvider.GetRequiredService<ILogger<CompositeDeliveryHostedService<TContext>>>();
            _hostedServices = [.. intents.Select(intent =>
            {
                 _logger.LogInformation("Initializing DeliveryHostedService for Publisher: {Publisher}, Intent: {Intent}", publisher, intent);
                    return new DeliveryHostedService<TContext>(
                        serviceProvider,
                        publisher,
                        intent);
            })];
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var tasks = _hostedServices.Select(s =>
            {
                _logger.LogInformation("Starting DeliveryHostedService for Publisher: {Publisher}, Intent: {Intent}", s.Publisher, s.Intent);
                return s.StartAsync(CancellationToken.None);
            });
            await Task.WhenAll(tasks);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var tasks = _hostedServices.Select(s =>
            {
                _logger.LogInformation("Stopping DeliveryHostedService for Publisher: {Publisher}, Intent: {Intent}", s.Publisher, s.Intent);
                return s.StopAsync(cancellationToken);
            });
            await Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            foreach (var service in _hostedServices)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
