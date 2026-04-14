using System.Diagnostics;
using System.Threading.Channels;
using Juice.EventBus.Subscriptions;
using Juice.MediatR;
using Juice.Messaging.Integrations;
using Juice.Messaging.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Messaging.Local.Internal
{
    /// <summary>
    /// Hosted service that continuously drains the <c>"local-channel"</c> in-memory queue
    /// and dispatches each message to registered in-process handlers concurrently.
    /// <para>
    /// A single reader loop reads messages from the channel sequentially; each message is
    /// dispatched as a new <see cref="Task.Run"/> invocation so that multiple handlers can
    /// execute in parallel. Concurrency is optionally bounded via
    /// <see cref="LocalChannelOptions.MaxConcurrency"/>.
    /// </para>
    /// <para>
    /// In-flight handler tasks use <see cref="CancellationToken.None"/> so they complete
    /// naturally on shutdown. The reader loop stops when the host cancels
    /// <c>stoppingToken</c> — no new messages are accepted after that point.
    /// </para>
    /// </summary>
    internal sealed class LocalChannelBackgroundService : BackgroundService
    {
        private const string PublisherKey = "local-channel";

        private readonly ChannelReader<ChannelEnvelope> _reader;
        private readonly IOptions<LocalChannelOptions> _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LocalChannelBackgroundService> _logger;

        public LocalChannelBackgroundService(
            ChannelReader<ChannelEnvelope> reader,
            IOptions<LocalChannelOptions> options,
            IServiceScopeFactory scopeFactory,
            ILogger<LocalChannelBackgroundService> logger)
        {
            _reader = reader;
            _options = options;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var maxConcurrency = _options.Value.MaxConcurrency;
            SemaphoreSlim? semaphore = maxConcurrency.HasValue && maxConcurrency.Value > 0
                ? new SemaphoreSlim(maxConcurrency.Value, maxConcurrency.Value)
                : null;

            await foreach (var envelope in _reader.ReadAllAsync(stoppingToken))
            {
                if (semaphore != null)
                {
                    await semaphore.WaitAsync(stoppingToken);
                }

                _ = Task.Run(async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    var message = envelope.Message;

                    // Each dispatch hop establishes a fresh execution context to maintain the
                    // causal chain. When the envelope carries a context snapshot we ALWAYS
                    // restore it — even if a parent AsyncLocal context was inherited — so that
                    // CausationId correctly reflects the publisher's ExecutionId and a new
                    // ExecutionId is generated for this hop.
                    // AsyncLocal values flow downward into Task.Run, so relying on
                    // !IsInitialized would skip the restore when called from an initialized
                    // scope (e.g. tests, ASP.NET request handlers).
                    // If no snapshot was captured, fall back to generating a fresh context
                    // only when no context is already present.
                    var needsContext = envelope.Context != null || !MessageContext.IsInitialized;
                    if (needsContext)
                    {
                        var newExecutionId = Guid.NewGuid().ToString();
                        _logger.LogDebug(
                            "Dispatch context: CorrelationId={CorrelationId} CausationId={CausationId} ExecutionId={ExecutionId} MessageType={MessageType}",
                            envelope.Context?.CorrelationId ?? "(generated)",
                            envelope.Context?.ExecutionId ?? "(none)",
                            newExecutionId,
                            message.GetType().Name);
                        MessageContext.Initialize(
                            correlationId: envelope.Context?.CorrelationId ?? Guid.NewGuid().ToString(),
                            causationId: envelope.Context?.ExecutionId,
                            executionId: newExecutionId,
                            source: envelope.Context?.Source ?? string.Empty);
                    }

                    try
                    {
                        LocalChannelMetrics.IncrementDeliveryAttempt(PublisherKey);

                        using var scope = _scopeFactory.CreateScope();
                        var sp = scope.ServiceProvider;

                        var dispatchSucceeded = true;
                        if (message is INotification notification)
                        {
                            _logger.LogDebug("Dispatching INotification {MessageType}", message.GetType().Name);
                            var publisher = sp.GetRequiredService<IMediator>();
                            await LocalDispatchHelper.DispatchNotificationAsync(publisher, notification, CancellationToken.None);
                        }
                        else if (message is IIntegrationEvent integrationEvent)
                        {
                            _logger.LogDebug("Dispatching IIntegrationEvent {MessageType}", message.GetType().Name);
                            var dispatcher = sp.GetRequiredService<IntegrationEventDispatcher>();
                            var subsManager = sp.GetKeyedService<ISubscriptionsManager>("local");
                            var result = await LocalDispatchHelper.DispatchIntegrationEventAsync(sp, dispatcher, subsManager, integrationEvent, CancellationToken.None);
                            dispatchSucceeded = result != Integrations.EventDispatchResult.Failure;
                        }
                        else
                        {
                            _logger.LogWarning("Message type {MessageType} is neither INotification nor IIntegrationEvent — skipped",
                                message.GetType().Name);
                        }

                        // For "local" route messages, mark the outbox delivery records as Published
                        // so the background delivery processor does not re-process them.
                        // Only mark Published when dispatch succeeded — if any handler failed,
                        // the records remain NotPublished and the delivery processor will retry.
                        // If marking fails (e.g. DB unavailable), the records remain NotPublished
                        // and the delivery processor will pick them up on its next cycle.
                        if (dispatchSucceeded && envelope.LocalDeliveryIds is { Count: > 0 })
                        {
                            var repo = sp.GetService<IOutboxRepository>();
                            if (repo != null)
                            {
                                try
                                {
                                    foreach (var deliveryId in envelope.LocalDeliveryIds)
                                    {
                                        await repo.MarkAsPublishedAsync(deliveryId, CancellationToken.None);
                                    }
                                }
                                catch (Exception markEx)
                                {
                                    _logger.LogWarning(markEx,
                                        "Failed to mark local delivery Published for message {MessageId} — " +
                                        "delivery will be retried by background processor",
                                        message.MessageId);
                                }
                            }
                        }

                        stopwatch.Stop();
                        LocalChannelMetrics.RecordDeliveryLatency(PublisherKey, stopwatch.Elapsed);
                        if (dispatchSucceeded)
                        {
                            LocalChannelMetrics.IncrementDeliverySuccess(PublisherKey);
                            _logger.LogDebug("Dispatched {MessageType} in {Elapsed}ms", message.GetType().Name, stopwatch.ElapsedMilliseconds);
                        }
                        else
                        {
                            LocalChannelMetrics.IncrementDeliveryFailure(PublisherKey, nameof(EventDispatchResult.Failure));
                            _logger.LogWarning("Dispatch of {MessageType} (id={MessageId}) failed — one or more handlers returned a failure result. Delivery remains NotPublished for retry.",
                                message.GetType().Name, message.MessageId);
                        }
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        _logger.LogError(ex,
                            "Error dispatching {MessageType} on {PublisherKey}. {Message}",
                            message.GetType().Name, PublisherKey, ex.Message);
                        LocalChannelMetrics.IncrementDeliveryFailure(PublisherKey, ex.GetType().Name);
                    }
                    finally
                    {
                        if (needsContext)
                        {
                            MessageContext.Clear();
                        }
                        semaphore?.Release();
                    }
                }, CancellationToken.None);
            }
        }
    }
}
