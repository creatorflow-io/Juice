using Juice.EventBus.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Outbox.Delivery.Processing
{
    /// <summary>
    /// Self-contained worker for a specific Publisher × Intent combination.
    /// Owns policy resolution and batch processing. Ready to execute after <see cref="InitializeAsync"/>.
    /// Intended to be shared between different background service implementations (basic polling, scheduled, etc.).
    /// </summary>
    public sealed class DeliveryWorker<TContext>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDeliveryPolicyResolver _policyResolver;
        private readonly ILogger _logger;

        public string Publisher { get; }
        public string Intent { get; }

        /// <summary>
        /// The resolved delivery policy. Available after <see cref="InitializeAsync"/> completes.
        /// </summary>
        public DeliveryPolicy Policy { get; private set; } = DeliveryPolicy.Default;

        public DeliveryWorker(
            IServiceProvider serviceProvider,
            string publisher,
            string intent,
            IDeliveryPolicyResolver policyResolver,
            ILogger<DeliveryWorker<TContext>> logger)
        {
            _serviceProvider = serviceProvider;
            Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            _policyResolver = policyResolver;
            _logger = logger;
        }

        /// <summary>
        /// Resolves the delivery policy once. Must be called before the processing loop starts.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            Policy = await _policyResolver.GetPolicyAsync(
                new DeliveryContext(Publisher, Intent, typeof(TContext).Name), cancellationToken);
        }

        /// <summary>
        /// Retrieves and processes a single batch of deliveries within a new service scope.
        /// </summary>
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var intent = scope.ServiceProvider.GetRequiredKeyedService<IDeliveryIntent<TContext>>(Intent);
            var publisher = scope.ServiceProvider.GetRequiredKeyedService<ITransportPublisher>(Publisher);
            var processor = scope.ServiceProvider.GetRequiredService<DeliveryProcessor<TContext>>();
            processor.Configure(publisher, Policy);

            var deliveries = await intent.RetrieveDeliveriesAsync(Publisher, Policy, cancellationToken);
            _logger.LogInformation("Retrieved {Count} deliveries for Publisher: {Publisher}, Intent: {Intent}",
                deliveries.Count(), Publisher, Intent);
            if (deliveries.Any())
            {
                await processor.ProcessAsync([.. deliveries], cancellationToken);
            }
        }
    }
}
