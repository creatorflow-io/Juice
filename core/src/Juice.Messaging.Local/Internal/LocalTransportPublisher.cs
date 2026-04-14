using System.Collections.Concurrent;
using System.Diagnostics;
using Juice.EventBus.Publishing;
using Juice.EventBus.Subscriptions;
using Juice.MediatR;
using Juice.Messaging.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Local.Internal
{
    /// <summary>
    /// Scoped <see cref="ITransportPublisher"/> registered under the reserved key <c>"local"</c>.
    /// Called by the existing <c>DeliveryHostedService</c> when processing outbox deliveries
    /// with <c>PublisherKey = "local"</c>. Deserializes the payload and dispatches to in-process
    /// handlers using the same mechanism as the local-channel path.
    /// </summary>
    internal sealed class LocalTransportPublisher : ITransportPublisher
    {
        public string Key => "local";

        private static readonly ConcurrentDictionary<string, Type?> _typeCache = new();

        private readonly IMessageSerializer _serializer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IntegrationEventDispatcher _dispatcher;
        private readonly ILogger<LocalTransportPublisher> _logger;

        public LocalTransportPublisher(
            IMessageSerializer serializer,
            IServiceScopeFactory scopeFactory,
            IntegrationEventDispatcher dispatcher,
            ILogger<LocalTransportPublisher> logger)
        {
            _serializer = serializer;
            _scopeFactory = scopeFactory;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        private static string? TryGetHeader(IDictionary<string, object?>? headers, string key)
            => headers != null && headers.TryGetValue(key, out var value) ? value?.ToString() : null;

        /// <summary>
        /// Deserializes the message payload. First tries <c>$type</c>-based deserialization
        /// (works for assemblies registered in <see cref="MessageSerializerOptions"/>).
        /// If that fails, falls back to resolving the concrete type from the
        /// <c>x-message-type</c> header by scanning loaded assemblies.
        /// </summary>
        private IMessage DeserializeMessage(byte[] payload, PublishContext context)
        {
            // First try: $type-based deserialization (handles Juice.* and configured assemblies)
            try
            {
                var message = _serializer.DeserializeFromUtf8Bytes<IMessage>(payload);
                if (message != null) return message;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Failed to deserialize message {MessageId} via $type metadata, " +
                    "falling back to x-message-type header resolution",
                    context.MessageId);
            }

            // Fallback: resolve type from outbox headers.
            // Try x-message-clr-type first (assembly-qualified, unambiguous),
            // then x-message-type (short name, scan loaded assemblies).
            var eventType = ResolveEventType(context);
            if (eventType == null)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize message payload for delivery {context.MessageId}. " +
                    $"Type could not be resolved from headers. " +
                    $"x-message-clr-type='{TryGetHeader(context.Headers, "x-message-clr-type")}', " +
                    $"x-message-type='{TryGetHeader(context.Headers, "x-message-type")}'.");
            }

            return _serializer.DeserializeFromUtf8Bytes<IMessage>(payload, eventType)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize message payload for delivery {context.MessageId} " +
                    $"as type '{eventType.FullName}'.");
        }

        private Type? ResolveEventType(PublishContext context)
        {
            // 1. Try assembly-qualified type name (unambiguous)
            var clrTypeName = TryGetHeader(context.Headers, "x-message-clr-type");
            if (!string.IsNullOrEmpty(clrTypeName))
            {
                var type = Type.GetType(clrTypeName);
                if (type != null && typeof(IMessage).IsAssignableFrom(type))
                    return type;

                _logger.LogDebug(
                    "x-message-clr-type '{ClrType}' could not be resolved, falling back to x-message-type",
                    clrTypeName);
            }

            // 2. Fall back to short name scan (cached)
            var shortName = TryGetHeader(context.Headers, "x-message-type");
            if (string.IsNullOrEmpty(shortName))
                return null;

            return _typeCache.GetOrAdd(shortName, name =>
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return []; }
                    })
                    .FirstOrDefault(t => t.Name == name && typeof(IMessage).IsAssignableFrom(t)));
        }

        public async ValueTask PublishAsync(
            byte[] payload,
            PublishContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            LocalChannelMetrics.IncrementDeliveryAttempt(Key);

            var message = DeserializeMessage(payload, context);

            // Restore MessageContext from outbox headers so that the idempotency key
            // ("{Source}:{MessageId}") matches across immediate dispatch and delivery retry
            // paths. Always restore when headers are present — the delivery loop runs on
            // background threads but the guard must not rely on !IsInitialized because
            // AsyncLocal values propagate into child scopes (e.g. from tests or middleware).
            var hasHeaders = context.Headers != null;
            var needsContext = hasHeaders || !MessageContext.IsInitialized;
            if (needsContext)
            {
                var headers = context.Headers;
                MessageContext.Initialize(
                    correlationId: TryGetHeader(headers, "x-correlation-id")
                        ?? Guid.NewGuid().ToString(),
                    causationId: TryGetHeader(headers, "x-causation-id"),
                    executionId: Guid.NewGuid().ToString(),
                    source: TryGetHeader(headers, "x-source") ?? Key);
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                if (message is INotification notification)
                {
                    var mediator = scope.ServiceProvider.GetService<IMediator>()
                        ?? throw new InvalidOperationException(
                            $"IMediator is not registered. Call services.AddMediatR() to dispatch " +
                            $"INotification messages through the '{Key}' transport publisher.");
                    await LocalDispatchHelper.DispatchNotificationAsync(mediator, notification, cancellationToken);
                }
                else if (message is IIntegrationEvent integrationEvent)
                {
                    var subsManager = scope.ServiceProvider.GetKeyedService<ISubscriptionsManager>("local");
                    var result = await LocalDispatchHelper.DispatchIntegrationEventAsync(
                        scope.ServiceProvider, _dispatcher, subsManager, integrationEvent, cancellationToken);
                    if (result == Integrations.EventDispatchResult.Duplicated)
                    {
                        // Phase 1 (LocalChannelBackgroundService) already processed this event but
                        // did not mark the outbox record Published (e.g. DB was unavailable during marking).
                        // The delivery processor picked it up as NotPublished and called this publisher.
                        // Log a warning so operators can distinguish this fallback from normal delivery.
                        _logger.LogWarning(
                            "Event {EventName} (MessageId={MessageId}) was already processed in phase 1 immediate dispatch " +
                            "but its outbox delivery was not marked Published — completing via phase 2 fallback. PublisherKey={PublisherKey}.",
                            integrationEvent.EventName, integrationEvent.MessageId, Key);
                        LocalChannelMetrics.IncrementPhase1Fallback(Key);
                        // Do not throw — DeliveryProcessor marks the record Published via the normal path.
                    }
                    else if (result == Integrations.EventDispatchResult.Failure)
                    {
                        throw new InvalidOperationException(
                            $"One or more handlers failed to process event {integrationEvent.GetType().Name} (MessageId={integrationEvent.MessageId})");
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Message type {MessageType} is neither INotification nor IIntegrationEvent — skipped",
                        message.GetType().Name);
                }

                stopwatch.Stop();
                LocalChannelMetrics.RecordDeliveryLatency(Key, stopwatch.Elapsed);
                LocalChannelMetrics.IncrementDeliverySuccess(Key);
            }
            catch
            {
                stopwatch.Stop();
                LocalChannelMetrics.RecordDeliveryLatency(Key, stopwatch.Elapsed);
                throw;
            }
            finally
            {
                if (needsContext)
                {
                    MessageContext.Clear();
                }
            }
        }
    }
}
