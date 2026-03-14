using System.Diagnostics;
using System.Threading.Channels;
using Juice.MediatR;
using Juice.Messaging.Integrations;
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

        private readonly ChannelReader<IMessage> _reader;
        private readonly IOptions<LocalChannelOptions> _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LocalChannelBackgroundService> _logger;

        public LocalChannelBackgroundService(
            ChannelReader<IMessage> reader,
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

            await foreach (var message in _reader.ReadAllAsync(stoppingToken))
            {
                if (semaphore != null)
                {
                    await semaphore.WaitAsync(stoppingToken);
                }

                _ = Task.Run(async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        LocalChannelMetrics.IncrementDeliveryAttempt(PublisherKey);

                        using var scope = _scopeFactory.CreateScope();
                        var sp = scope.ServiceProvider;

                        if (message is INotification notification)
                        {
                            var publisher = sp.GetRequiredService<IMediator>();
                            await LocalDispatchHelper.DispatchNotificationAsync(publisher, notification, CancellationToken.None);
                        }
                        else if (message is IIntegrationEvent integrationEvent)
                        {
                            var dispatcher = sp.GetRequiredService<IntegrationEventDispatcher>();
                            await LocalDispatchHelper.DispatchIntegrationEventAsync(sp, dispatcher, integrationEvent, CancellationToken.None);
                        }
                        else
                        {
                            _logger.LogWarning("Message type {MessageType} is neither INotification nor IIntegrationEvent — skipped",
                                message.GetType().Name);
                        }

                        stopwatch.Stop();
                        LocalChannelMetrics.RecordDeliveryLatency(PublisherKey, stopwatch.Elapsed);
                        LocalChannelMetrics.IncrementDeliverySuccess(PublisherKey);
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
                        semaphore?.Release();
                    }
                }, CancellationToken.None);
            }
        }
    }
}
