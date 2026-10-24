using System.Collections.Concurrent;
using System.Text;
using Juice.EventBus.Extensions;
using Juice.EventBus.NATS.Infrastructure;
using Juice.EventBus.NATS.Policies;
using Juice.EventBus.Policies;
using Juice.EventBus.Subscriptions;
using Juice.Messaging;
using Juice.Messaging.Integrations;
using Juice.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Juice.EventBus.NATS.Consuming
{
    internal sealed class JetStreamConsumerEngine
    {
        /// <summary>
        /// Redelivery delay used when no retry policy provider is registered or processing throws unexpectedly.
        /// Never zero, so an always-failing message cannot spin in a tight loop.
        /// </summary>
        internal static readonly TimeSpan FallbackRedeliveryDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ParkingFailureDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxReconnectBackoff = TimeSpan.FromSeconds(30);

        private readonly IntegrationEventDispatcher _dispatcher;
        private readonly IRetryPolicyProvider<JetStreamRetryPolicy>? _retryPolicyProvider;
        private readonly IMessageSerializer _eventSerializer;
        private readonly ILogger _logger;
        private readonly IServiceProvider _keyedService;

        private string _serviceKey = default!;
        private JetStreamConsumerEndpoint _endpoint = default!;
        private ISubscriptionsManager _subscriptionsManager = default!;
        private IJetStreamConnection? _connection;

        private CancellationTokenSource? _loopCts;
        private readonly CancellationTokenSource _handlerCts = new();
        private readonly ConcurrentDictionary<Task, byte> _inFlight = new();
        private SemaphoreSlim _concurrency = default!;

        /// <summary>
        /// Publishes a parked copy: (subject, data, headers, token). Defaults to a JetStream publish;
        /// replaced by unit tests.
        /// </summary>
        internal Func<string, byte[], NatsHeaders, CancellationToken, ValueTask>? ParkingPublisher { get; set; }

        public JetStreamConsumerEngine(
            IServiceProvider keyedService,
            IntegrationEventDispatcher dispatcher,
            IMessageSerializer eventSerializer,
            ILogger<JetStreamConsumerEngine> logger,
            IRetryPolicyProvider<JetStreamRetryPolicy>? retryPolicyProvider = default
            )
        {
            _retryPolicyProvider = retryPolicyProvider;
            _dispatcher = dispatcher;
            _eventSerializer = eventSerializer;
            _logger = logger;
            _keyedService = keyedService;
        }

        internal void Configure(string serviceKey, JetStreamConsumerEndpoint endpoint, ISubscriptionsManager subscriptionsManager)
        {
            _serviceKey = serviceKey;
            _endpoint = endpoint;
            _subscriptionsManager = subscriptionsManager;
            _concurrency = new SemaphoreSlim(endpoint.MaxConcurrency, endpoint.MaxConcurrency);
        }

        #region Consume loop

        public async Task StartAsync(
            string serviceKey,
            JetStreamConsumerEndpoint endpoint,
            ISubscriptionsManager subscriptionsManager,
            CancellationToken cancellationToken)
        {
            Configure(serviceKey, endpoint, subscriptionsManager);
            _connection = _keyedService.GetKeyedService<IJetStreamConnection>(endpoint.ConnectionName)
                ?? throw new InvalidOperationException($"JetStream connection with name '{endpoint.ConnectionName}' is not registered.");

            _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _loopCts.Token;
            var backoff = TimeSpan.FromSeconds(1);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _connection.EnsureConnectedAsync(token);
                    var consumer = await EnsureConsumerAsync(token);
                    backoff = TimeSpan.FromSeconds(1);

                    _logger.LogInformation("[{Stream}/{Durable}] Starting JetStream consume with filters {Filters}.",
                        _endpoint.Stream, _endpoint.DurableName, string.Join(", ", _endpoint.FilterSubjects));

                    // Never block this loop: the client buffers pulled messages in a bounded channel and drops
                    // new ones when it is full. Outstanding messages are bounded server-side by MaxAckPending
                    // (MaxConcurrency * 2); the semaphore inside each task bounds concurrent handlers.
                    await foreach (var msg in consumer.ConsumeAsync<byte[]>(
                        opts: new NatsJSConsumeOpts { MaxMsgs = _endpoint.MaxConcurrency },
                        cancellationToken: token))
                    {
                        var message = new NatsJSMessageAdapter(msg);
                        var task = Task.Run(async () =>
                        {
                            if (!await WaitForSlotAsync(message))
                            {
                                return; // shutting down; the message is redelivered after AckWait
                            }
                            try
                            {
                                await HandleAsync(message, _handlerCts.Token);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Unhandled error processing {Subject}. {Message}", message.Subject, ex.Message);
                            }
                            finally
                            {
                                ReleaseSlot();
                            }
                        }, CancellationToken.None);
                        _inFlight.TryAdd(task, 0);
                        _ = task.ContinueWith(t => _inFlight.TryRemove(t, out _), TaskScheduler.Default);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (JetStreamTopologyException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{Stream}/{Durable}] JetStream consume interrupted. Restarting in {Delay}. {Message}",
                        _endpoint.Stream, _endpoint.DurableName, backoff, ex.Message);
                    try
                    {
                        await Task.Delay(backoff, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxReconnectBackoff.Ticks));
                }
            }
        }

        /// <summary>
        /// Wait for a handler slot, sending progress meanwhile so a message pulled ahead is not
        /// redelivered by the server while it is still queued here. False when shutting down.
        /// </summary>
        internal async Task<bool> WaitForSlotAsync(IJetStreamMessage message)
        {
            if (_concurrency.Wait(0))
            {
                return true;
            }
            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(_handlerCts.Token);
            var progress = SendProgressAsync(message, progressCts.Token);
            try
            {
                await _concurrency.WaitAsync(_handlerCts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                progressCts.Cancel();
                await progress;
            }
        }

        internal void ReleaseSlot() => _concurrency.Release();

        /// <summary>
        /// The server stops redelivering after MaxDeliver, so it must leave room for every retry tier
        /// plus the final delivery that parks or terminates the message.
        /// </summary>
        internal async Task ValidateMaxDeliverAsync(long maxDeliver)
        {
            if (maxDeliver <= 0 || _retryPolicyProvider == null)
            {
                return;
            }
            var policy = await _retryPolicyProvider.GetRetryPolicyForSourceAsync(_endpoint.Destination, (int)Math.Min(maxDeliver, int.MaxValue));
            if (policy != null && !policy.IsMaxRetryReached)
            {
                throw new JetStreamTopologyException(
                    $"JetStream consumer '{_endpoint.DurableName}' on stream '{_endpoint.Stream}' has MaxDeliver {maxDeliver}, which does not exceed the retry tiers of '{_endpoint.Destination}'. " +
                    "The server would stop redelivering before the message is parked or terminated; raise MaxDeliver or use -1 (unlimited).");
            }
        }

        /// <summary>
        /// Stop pulling new messages and wait for in-flight handlers until <paramref name="shutdownToken"/> is cancelled.
        /// Handlers that do not finish in time leave their message unacknowledged for redelivery.
        /// </summary>
        public async Task StopAsync(CancellationToken shutdownToken)
        {
            _loopCts?.Cancel();
            var inFlight = _inFlight.Keys.ToArray();
            if (inFlight.Length > 0)
            {
                _logger.LogInformation("[{Stream}/{Durable}] Waiting for {Count} in-flight messages.",
                    _endpoint?.Stream, _endpoint?.DurableName, inFlight.Length);
                try
                {
                    await Task.WhenAll(inFlight).WaitAsync(shutdownToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("[{Stream}/{Durable}] Shutdown grace period elapsed; unfinished messages will be redelivered.",
                        _endpoint?.Stream, _endpoint?.DurableName);
                }
                catch (Exception)
                {
                    // handler failures are already logged by HandleAsync
                }
            }
            _handlerCts.Cancel();
        }

        private async Task<INatsJSConsumer> EnsureConsumerAsync(CancellationToken cancellationToken)
        {
            var js = _connection!.JetStream;
            try
            {
                var consumer = await js.GetConsumerAsync(_endpoint.Stream, _endpoint.DurableName, cancellationToken);
                var existing = consumer.Info.Config.FilterSubjects?.ToArray()
                    ?? (string.IsNullOrEmpty(consumer.Info.Config.FilterSubject) ? [] : [consumer.Info.Config.FilterSubject]);
                if (!existing.OrderBy(s => s).SequenceEqual(_endpoint.FilterSubjects.OrderBy(s => s)))
                {
                    _logger.LogWarning("[{Stream}/{Durable}] Existing consumer filters [{Existing}] differ from subscriptions [{Expected}]. The consumer is not updated.",
                        _endpoint.Stream, _endpoint.DurableName, string.Join(", ", existing), string.Join(", ", _endpoint.FilterSubjects));
                }
                await ValidateMaxDeliverAsync(consumer.Info.Config.MaxDeliver);
                return consumer;
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                if (ex.Error.ErrCode == 10059)
                {
                    throw new JetStreamTopologyException($"JetStream stream '{_endpoint.Stream}' does not exist (consumer '{_endpoint.DurableName}').");
                }
                if (_endpoint.Provisioning != JetStreamProvisioning.CreateIfMissing)
                {
                    throw new JetStreamTopologyException($"JetStream consumer '{_endpoint.DurableName}' on stream '{_endpoint.Stream}' does not exist (Provisioning=VerifyOnly).");
                }
            }

            await ValidateMaxDeliverAsync(_endpoint.MaxDeliver);
            var config = new ConsumerConfig(_endpoint.DurableName)
            {
                DurableName = _endpoint.DurableName,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                AckWait = _endpoint.AckWait,
                MaxAckPending = _endpoint.MaxConcurrency * 2,
                MaxDeliver = _endpoint.MaxDeliver
            };
            if (_endpoint.FilterSubjects.Count == 1)
            {
                config.FilterSubject = _endpoint.FilterSubjects[0];
            }
            else if (_endpoint.FilterSubjects.Count > 1)
            {
                config.FilterSubjects = _endpoint.FilterSubjects.ToList();
            }
            try
            {
                var created = await js.CreateOrUpdateConsumerAsync(_endpoint.Stream, config, cancellationToken);
                _logger.LogInformation("[{Stream}/{Durable}] Created JetStream consumer.", _endpoint.Stream, _endpoint.DurableName);
                return created;
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                throw new JetStreamTopologyException($"JetStream stream '{_endpoint.Stream}' does not exist (consumer '{_endpoint.DurableName}').");
            }
        }

        #endregion

        #region Processing

        internal async Task HandleAsync(IJetStreamMessage msg, CancellationToken cancellationToken)
        {
            // Everything runs inside the try: an exception escaping here would leave the message
            // unacknowledged until AckWait instead of being redelivered after a short delay.
            try
            {
                var headers = msg.Headers;
                var eventName = headers.GetHeaderString("x-original-routing-key")
                    ?? headers.GetHeaderString("x-message-name")
                    ?? JetStreamNaming.ExtractEventName(msg.Subject, _endpoint.Destination);
                var attempts = (int)msg.NumDelivered;

                using var _ = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["EventName"] = eventName,
                    ["Attempts"] = attempts
                });

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("[{Stream}/{Durable}] Received event: {EventName} {MessageId} on {Subject}, attempt {Attempts}",
                        _endpoint.Stream, _endpoint.DurableName, eventName, headers.GetHeaderString("x-message-id"), msg.Subject, attempts);
                }

                var eventType = await _subscriptionsManager.GetEventTypeByNameAsync(eventName);
                if (eventType == null)
                {
                    _logger.LogWarning("No event type found for event: {EventName}", eventName);
                    await HandleNotHandledAsync(msg, eventName, cancellationToken);
                    return;
                }

                IIntegrationEvent? integrationEvent = null;
                try
                {
                    integrationEvent = _eventSerializer.Deserialize<IIntegrationEvent>(Encoding.UTF8.GetString(msg.Data.Span), eventType);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize message to {EventType}: {Message}", eventType.Name, ex.Message);
                }
                if (integrationEvent == null)
                {
                    await HandleNotProcessableAsync(msg, eventName, cancellationToken);
                    return;
                }

                var correlationId = headers.GetHeaderString("x-correlation-id")
                    ?? StringIdGenerator.Instance.GenerateUniqueId();
                var messageId = headers.GetHeaderString("x-message-id")
                    ?? integrationEvent.MessageId.ToString();
                var tenantId = headers.GetHeaderString("x-tenant-id");
                var handlers = await _subscriptionsManager.GetHandlersForEventAsync(eventName);

                EventDispatchResult result;
                using (var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    var progress = SendProgressAsync(msg, progressCts.Token);
                    try
                    {
                        MessageContext.Initialize(
                            correlationId: correlationId,
                            causationId: messageId,
                            executionId: StringIdGenerator.Instance.GenerateUniqueId(),
                            source: _serviceKey
                            );

                        result = await _dispatcher.DispatchAsync(integrationEvent, new EventDispatchContext(handlers, eventName, tenantId, _serviceKey));
                    }
                    finally
                    {
                        MessageContext.Clear();
                        progressCts.Cancel();
                        await progress;
                    }
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("[{Stream}/{Durable}] Dispatched event: {EventName} {MessageId}, result {Result}",
                        _endpoint.Stream, _endpoint.DurableName, eventName, messageId, result);
                }
                switch (result)
                {
                    case EventDispatchResult.Success:
                    case EventDispatchResult.Duplicated:
                        await msg.AckAsync(cancellationToken);
                        break;
                    case EventDispatchResult.NotHandled:
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("No handler processed JetStream event: {EventName}", eventName);
                        }
                        await HandleNotHandledAsync(msg, eventName, cancellationToken);
                        break;
                    default:
                        await HandleFailureAsync(msg, eventName, attempts, cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // shutting down: leave the message unacknowledged for redelivery
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "----- ERROR Processing message {Subject}. {Message}", msg.Subject, ex.Message);
                try
                {
                    await msg.NakAsync(FallbackRedeliveryDelay, cancellationToken);
                }
                catch (Exception nakEx)
                {
                    _logger.LogWarning(nakEx, "Could not NAK message {Subject}. It will be redelivered after AckWait.", msg.Subject);
                }
            }
        }

        private async Task SendProgressAsync(IJetStreamMessage msg, CancellationToken cancellationToken)
        {
            var interval = TimeSpan.FromTicks(_endpoint.AckWait.Ticks / 2);
            try
            {
                while (true)
                {
                    await Task.Delay(interval, cancellationToken);
                    await msg.AckProgressAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send progress for {Subject}. {Message}", msg.Subject, ex.Message);
            }
        }

        private async Task HandleFailureAsync(IJetStreamMessage msg, string eventName, int attempts, CancellationToken cancellationToken)
        {
            if (_retryPolicyProvider == null)
            {
                await msg.NakAsync(FallbackRedeliveryDelay, cancellationToken);
                return;
            }

            var retryPolicy = await _retryPolicyProvider.GetRetryPolicyForSourceAsync(_endpoint.Destination, attempts);
            if (retryPolicy == null || (retryPolicy.IsMaxRetryReached && !retryPolicy.IsParkingEnabled))
            {
                _logger.LogError("Max processing retries reached for event: {EventName}, message: {MessageId}, attempts: {Attempts}. Terminated.",
                    eventName, msg.Headers.GetHeaderString("x-message-id"), attempts);
                await msg.AckTerminateAsync(cancellationToken);
                return;
            }
            if (retryPolicy.IsMaxRetryReached)
            {
                await ParkAsync(msg, eventName, $"MaxRetriesReached_Attempts_{attempts}", retryPolicy, cancellationToken);
                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Retrying event: {EventName}, attempts: {Attempts}, delay: {Delay}", eventName, attempts, retryPolicy.Delay);
            }
            JetStreamMetrics.IncrementRetry(_endpoint.Destination);
            await msg.NakAsync(retryPolicy.Delay!.Value, cancellationToken);
        }

        private async Task HandleNotHandledAsync(IJetStreamMessage msg, string eventName, CancellationToken cancellationToken)
        {
            if (_endpoint.NotHandledBehavior == NotHandledBehavior.Park)
            {
                await ParkAsync(msg, eventName, "NotHandled", null, cancellationToken);
                return;
            }
            _logger.LogWarning("[{Stream}/{Durable}] Event {EventName} is not handled by this consumer. Terminated.",
                _endpoint.Stream, _endpoint.DurableName, eventName);
            await msg.AckTerminateAsync(cancellationToken);
        }

        private async Task HandleNotProcessableAsync(IJetStreamMessage msg, string eventName, CancellationToken cancellationToken)
        {
            var retryPolicy = _retryPolicyProvider == null ? null
                : await _retryPolicyProvider.GetRetryPolicyForSourceAsync(_endpoint.Destination, Math.Max(1, (int)msg.NumDelivered));
            if (retryPolicy is { IsParkingEnabled: true })
            {
                await ParkAsync(msg, eventName, "DeserializationFailed", retryPolicy, cancellationToken);
                return;
            }
            _logger.LogWarning("Event {EventName} on {Subject} cannot be deserialized and parking is disabled. Terminated.", eventName, msg.Subject);
            await msg.AckTerminateAsync(cancellationToken);
        }

        private async Task ParkAsync(IJetStreamMessage msg, string eventName, string reason,
            JetStreamRetryPolicy? retryPolicy, CancellationToken cancellationToken)
        {
            var prefix = retryPolicy?.ParkingPrefix
                ?? _endpoint.ParkingPrefix
                ?? JetStreamNaming.DefaultParkingPrefix(_endpoint.Destination);
            var subject = $"{prefix}.{eventName}";

            var headers = new Dictionary<string, object?>(msg.Headers);
            headers["x-attempts"] = msg.NumDelivered;
            headers["x-death-reason"] = reason;
            headers["x-death-timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            headers["x-original-queue"] = _endpoint.DurableName;
            if (!headers.ContainsKey("x-original-exchange"))
            {
                headers["x-original-exchange"] = _endpoint.Destination;
            }
            if (!headers.ContainsKey("x-original-routing-key"))
            {
                headers["x-original-routing-key"] = eventName;
            }
            // Nats-Msg-Id of the original would de-duplicate against itself in a shared window
            headers.Remove("Nats-Msg-Id");

            _logger.LogWarning(
                "Parking message. Stream: {Stream}, Durable: {Durable}, Event: {Event}, Subject: {Subject}, Reason: {Reason}",
                _endpoint.Stream, _endpoint.DurableName, eventName, subject, reason);
            try
            {
                var publisher = ParkingPublisher ?? PublishParkedAsync;
                await publisher(subject, msg.Data.ToArray(), JetStreamHeaders.Encode(headers), cancellationToken);
                await msg.AckAsync(cancellationToken);
                JetStreamMetrics.IncrementParked(_endpoint.Destination);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var delay = retryPolicy?.LastDelay ?? ParkingFailureDelay;
                _logger.LogError(ex, "Failed to park message {Subject} to {ParkingSubject}. Redelivering in {Delay}. {Message}",
                    msg.Subject, subject, delay, ex.Message);
                await msg.NakAsync(delay, cancellationToken);
            }
        }

        private async ValueTask PublishParkedAsync(string subject, byte[] data, NatsHeaders headers, CancellationToken cancellationToken)
        {
            var messageId = headers.TryGetValue("x-message-id", out var id) ? id.ToString() : null;
            var ack = await _connection!.JetStream.PublishAsync(subject, data,
                opts: new NatsJSPubOpts { MsgId = messageId == null ? null : $"{messageId}:parked" },
                headers: headers,
                cancellationToken: cancellationToken);
            if (ack.Error == null && ack.Duplicate)
            {
                return;
            }
            ack.EnsureSuccess();
        }

        #endregion
    }

    /// <summary>
    /// A required stream or consumer is missing or cannot be verified. Not retried by the consume loop.
    /// </summary>
    internal sealed class JetStreamTopologyException : InvalidOperationException
    {
        public JetStreamTopologyException(string message) : base(message)
        {
        }
    }
}
