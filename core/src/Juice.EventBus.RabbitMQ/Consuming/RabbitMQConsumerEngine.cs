using System.Text;
using Juice.EventBus.Policies;
using Juice.EventBus.RabbitMQ.Policies;
using Juice.EventBus.Subscriptions;
using Juice.Messaging;
using Juice.Messaging.Diagnostics;
using Juice.Messaging.Integrations;
using Juice.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Juice.EventBus.RabbitMQ.Consuming
{
    /// <summary>
    /// One consumer's lifecycle and message handling. It starts consuming, retrying with a backoff until it can; starts again when the broker
    /// closes its channel or cancels it (the client itself recovers a lost connection with its channels and consumers); and on stop cancels
    /// the consumer and waits for the messages being handled. Its state is in <see cref="RabbitMQConsumerStatus"/>.
    /// </summary>
    internal sealed class RabbitMQConsumerEngine
    {
        internal static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(1);
        internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

        private IRabbitMQPersistentConnection _persistentConnection = default!;

        private IChannel? _consumerChannel;
        private AsyncEventingBasicConsumer? _consumer;

        private string _queueName = default!;
        private ushort _qosPrefetchCount = 1;
        private RabbitMQConsumerEndpoint _endpoint = default!;
        private RabbitMQConsumerStatus? _status;

        private ISubscriptionsManager _subscriptionsManager = default!;
        private readonly IntegrationEventDispatcher _dispatcher;
        private readonly IRetryPolicyProvider<RetryPolicy>? _retryPolicyProvider;
        private readonly IMessageSerializer _eventSerializer;
        private readonly ILogger _logger;
        private readonly IServiceProvider _keyedService;
        private string _serviceKey = default!;
        private string _dlRoutingPattern = "{0}.parking";

        // one start, restart or stop at a time
        private readonly SemaphoreSlim _lifecycle = new(1, 1);
        private readonly CancellationTokenSource _lifetime = new();
        // 1 while a start loop runs: a loss seen meanwhile is that loop's to handle
        private int _restarting;
        // counts losses (a cancel, a channel shutdown), so a start that raced one doesn't report itself consuming
        private int _losses;
        // set when the channel shuts down, cleared when the consumer is registered again: a cancel in between is part of that shutdown
        private volatile bool _channelDown;
        private volatile bool _stopping;
        private int _inFlight;
        private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RabbitMQConsumerEngine(
            IServiceProvider keyedService,
            IntegrationEventDispatcher dispatcher,
            IMessageSerializer eventSerializer,
            ILogger<RabbitMQConsumerEngine> logger,
            IRetryPolicyProvider<RetryPolicy>? retryPolicyProvider = default
            )
        {
            _retryPolicyProvider = retryPolicyProvider;
            _dispatcher = dispatcher;
            _eventSerializer = eventSerializer;
            _logger = logger;
            _keyedService = keyedService;
        }

        private RabbitMQConsumerStatus Status => _status!;

        /// <summary>
        /// Starts consuming, retrying until it does or <paramref name="stoppingToken"/> is cancelled. Returns once consuming (or stopped);
        /// a consumer lost later starts again by itself.
        /// </summary>
        public async Task RunAsync(
            string serviceKey,
            RabbitMQConsumerEndpoint endpoint,
            ISubscriptionsManager subscriptionsManager,
            RabbitMQConsumerStatus status,
            CancellationToken stoppingToken)
        {
            _serviceKey = serviceKey;
            _endpoint = endpoint;
            _status = status;
            _persistentConnection = _keyedService.GetKeyedService<IRabbitMQPersistentConnection>(endpoint.ConnectionName)
                ?? throw new InvalidOperationException($"RabbitMQ connection with name '{endpoint.ConnectionName}' is not registered.");
            _queueName = endpoint.Queue;
            _dlRoutingPattern = endpoint.DLRoutingPattern ?? _dlRoutingPattern;
            _subscriptionsManager = subscriptionsManager;
            _qosPrefetchCount = endpoint.QosPrefetchCount;

            Interlocked.Exchange(ref _restarting, 1);
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _lifetime.Token);
                await KeepConsumingAsync(RabbitMQConsumerState.Starting, waitForRecovery: false, TimeSpan.Zero, linked.Token);
            }
            finally
            {
                Interlocked.Exchange(ref _restarting, 0);
            }
            if (!_stopping && !stoppingToken.IsCancellationRequested && !IsConsumingNow)
            {
                ScheduleRestart(waitForRecovery: false);
            }
        }

        /// <summary>
        /// Stops taking messages (the consumer is cancelled; any already sent ahead go back to the queue), waits until those being handled
        /// are settled or <paramref name="cancellationToken"/> (the host's shutdown timeout) ends, then closes the channel. A message still
        /// being handled then is redelivered by the broker.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _stopping = true;
            _lifetime.Cancel();
            var locked = false;
            try
            {
                try
                {
                    await _lifecycle.WaitAsync(cancellationToken);
                    locked = true;
                }
                catch (OperationCanceledException) { }

                var channel = _consumerChannel;
                var consumer = _consumer;
                if (channel is { IsOpen: true } && consumer is not null)
                {
                    foreach (var tag in consumer.ConsumerTags)
                    {
                        try
                        {
                            await channel.BasicCancelAsync(tag, noWait: false, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[{Queue}] Could not cancel consumer {ConsumerTag}", _queueName, tag);
                        }
                    }
                }
                var inFlight = Volatile.Read(ref _inFlight);
                if (inFlight > 0)
                {
                    _logger.LogInformation("[{Queue}] Waiting for {Count} message(s) being handled", _queueName, inFlight);
                    try
                    {
                        await _drained.Task.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("[{Queue}] Stopped with {Count} message(s) still being handled; the broker redelivers them",
                            _queueName, Volatile.Read(ref _inFlight));
                    }
                }
                // The client's close waits for the handler running on the channel, so it is bounded too: past the timeout it finishes
                // in the background (or with the connection, when the container disposes it).
                var teardown = TearDownAsync();
                try
                {
                    await teardown.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _ = teardown.ContinueWith(t => _logger.LogDebug(t.Exception, "[{Queue}] Closing the consumer channel failed", _queueName),
                        TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            finally
            {
                _status?.Stopped();
                if (locked)
                {
                    _lifecycle.Release();
                }
            }
        }

        #region Lifecycle

        /// <summary>
        /// Tries to consume until it does, the consumer is found consuming again (the client recovered it), or <paramref name="ct"/> ends.
        /// </summary>
        /// <param name="waitForRecovery">
        /// After a lost connection: the client recovers it with its channels and consumers, so a new consumer is only started once the
        /// connection has been back for two checks and the old one still isn't consuming (its queue went while the broker was away, say).
        /// </param>
        /// <param name="state">What the status says while it isn't consuming: starting, or recovering.</param>
        /// <param name="delay">The wait before the first try (zero: at once); it doubles up to <see cref="MaxRetryDelay"/>.</param>
        /// <param name="ct">Ends the loop.</param>
        private async Task KeepConsumingAsync(RabbitMQConsumerState state, bool waitForRecovery, TimeSpan delay, CancellationToken ct)
        {
            var connectedChecks = 0;
            while (!ct.IsCancellationRequested && !_stopping)
            {
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                delay = NextDelay(delay);
                if (IsConsumingNow)
                {
                    return;
                }
                if (waitForRecovery)
                {
                    if (!_persistentConnection.IsConnected)
                    {
                        connectedChecks = 0; // down again: the client's recovery starts over
                        continue;
                    }
                    if (++connectedChecks < 2)
                    {
                        continue;
                    }
                }
                try
                {
                    await _lifecycle.WaitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                try
                {
                    if (_stopping || IsConsumingNow)
                    {
                        return;
                    }
                    await StartConsumingAsync(ct);
                    return;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Status.Set(state, ex.Message);
                    _logger.LogWarning(ex, "[{Queue}] Could not start consuming ({Reason}); trying again in {Delay}s",
                        _queueName, ex.Message, delay.TotalSeconds);
                }
                finally
                {
                    _lifecycle.Release();
                }
            }
        }

        internal static TimeSpan NextDelay(TimeSpan delay)
            => delay <= TimeSpan.Zero ? FirstRetryDelay : TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));

        private async Task StartConsumingAsync(CancellationToken ct)
        {
            await TearDownAsync();
            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync(ct).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"RabbitMQ connection '{_endpoint.ConnectionName}' is not available.");
            }

            _logger.LogInformation("[{Queue}] Creating RabbitMQ consumer channel.", _queueName);
            // publisher confirms: a message moved to a retry tier or parking is only acked once the broker has its copy
            var channel = await _persistentConnection.CreateChannelAsync(Publishing.ChannelPool.PublisherChannelOptions, ct)
                ?? throw new InvalidOperationException($"RabbitMQ connection '{_endpoint.ConnectionName}' gave no channel.");
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += Consumer_ReceivedAsync;
            consumer.RegisteredAsync += OnRegisteredAsync;
            consumer.UnregisteredAsync += OnUnregisteredAsync;
            channel.ChannelShutdownAsync += OnChannelShutdownAsync;
            channel.CallbackExceptionAsync += OnCallbackExceptionAsync;
            _consumerChannel = channel;
            _consumer = consumer;
            var losses = 0;
            try
            {
                if (_endpoint.ParkingExchange is { } parking)
                {
                    // Parking into an exchange that isn't there would close this channel with every message it parks: wait for it here.
                    await channel.ExchangeDeclarePassiveAsync(parking, ct);
                }
                // Before consuming: a per-consumer prefetch applies only to consumers created after it. Set after, the broker sends the
                // whole queue ahead.
                await channel.BasicQosAsync(0, _qosPrefetchCount, false, ct);

                losses = Volatile.Read(ref _losses);
                _channelDown = false;
                _logger.LogInformation("Starting RabbitMQ basic consume queue {queueName}.", _queueName);
                await channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: ct);
            }
            catch
            {
                await TearDownAsync();
                throw;
            }
            if (Volatile.Read(ref _losses) == losses)
            {
                Status.Set(RabbitMQConsumerState.Consuming);
            }
        }

        /// <summary>Whether the consumer is registered on an open channel, from the client's own state rather than the status.</summary>
        private bool IsConsumingNow => _consumerChannel is { IsOpen: true } && _consumer is { IsRunning: true } && !_channelDown;

        /// <summary>Closes the current channel, if any, without it counting as a loss.</summary>
        private async Task TearDownAsync()
        {
            var channel = Interlocked.Exchange(ref _consumerChannel, null);
            var consumer = Interlocked.Exchange(ref _consumer, null);
            if (consumer is not null)
            {
                consumer.ReceivedAsync -= Consumer_ReceivedAsync;
                consumer.RegisteredAsync -= OnRegisteredAsync;
                consumer.UnregisteredAsync -= OnUnregisteredAsync;
            }
            if (channel is null)
            {
                return;
            }
            channel.ChannelShutdownAsync -= OnChannelShutdownAsync;
            channel.CallbackExceptionAsync -= OnCallbackExceptionAsync;
            try
            {
                // Always: closing is what drops the channel from the client's recovery records, even one that is already closed or
                // half-recovered, which would otherwise come back with a consumer of its own.
                await channel.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[{Queue}] Closing the consumer channel failed", _queueName);
            }
            channel.Dispose();
            // the connection is a shared keyed singleton (producers, other consumers): the container disposes it
        }

        /// <summary>Starts a loop that consumes again, unless one is running, which then sees the loss itself.</summary>
        private void ScheduleRestart(bool waitForRecovery)
        {
            if (_stopping || Interlocked.CompareExchange(ref _restarting, 1, 0) != 0)
            {
                return;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    await KeepConsumingAsync(RabbitMQConsumerState.Recovering, waitForRecovery, FirstRetryDelay, _lifetime.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Queue}] The consumer's restart loop failed", _queueName);
                }
                finally
                {
                    Interlocked.Exchange(ref _restarting, 0);
                }
                // a loss after the loop decided it was consuming, and before it let go
                if (!_stopping && !IsConsumingNow)
                {
                    ScheduleRestart(waitForRecovery);
                }
            });
        }

        private Task OnRegisteredAsync(object sender, ConsumerEventArgs e)
        {
            // also after the client recovers a lost connection and re-registers the consumer
            _channelDown = false;
            Status.Set(RabbitMQConsumerState.Consuming);
            return Task.CompletedTask;
        }

        private Task OnUnregisteredAsync(object sender, ConsumerEventArgs e)
        {
            // The client also reports a consumer unregistered when its channel shuts down (a lost connection, which it recovers): that is
            // the channel's shutdown to handle, and a new consumer here would run next to the recovered one.
            if (_stopping || _channelDown || sender is AsyncDefaultBasicConsumer { Channel.IsOpen: false })
            {
                return Task.CompletedTask;
            }
            // the broker cancelled it: its queue was deleted, or (quorum, stream) its leader moved
            Interlocked.Increment(ref _losses);
            _logger.LogWarning("[{Queue}] The broker cancelled the consumer; consuming again", _queueName);
            Status.Set(RabbitMQConsumerState.Recovering, "The broker cancelled the consumer (was its queue deleted?).");
            ScheduleRestart(waitForRecovery: false);
            return Task.CompletedTask;
        }

        private Task OnChannelShutdownAsync(object sender, ShutdownEventArgs e)
        {
            if (_stopping || e.Initiator == ShutdownInitiator.Application)
            {
                return Task.CompletedTask;
            }
            _channelDown = true;
            Interlocked.Increment(ref _losses);
            var channelError = IsChannelError(e);
            var reason = channelError
                ? $"The broker closed the consumer's channel: {e.ReplyCode} {e.ReplyText}"
                : $"The connection was lost: {e.ReplyCode} {e.ReplyText}";
            _logger.LogWarning("[{Queue}] {Reason}", _queueName, reason);
            Status.Set(RabbitMQConsumerState.Recovering, reason);
            // the client recovers a channel lost with its connection, not one the broker closed by itself
            ScheduleRestart(waitForRecovery: !channelError);
            return Task.CompletedTask;
        }

        private Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
        {
            _logger.LogWarning(e.Exception, "[{Queue}] A RabbitMQ consumer callback threw", _queueName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Whether the broker closed just this channel (a soft error: AMQP 311, 312, 313, 403, 404, 405 or 406, such as an ack timeout or a
        /// queue that's gone), which the client doesn't recover, rather than the connection, whose channels it does.
        /// </summary>
        internal static bool IsChannelError(ShutdownEventArgs e)
            => e.Initiator == ShutdownInitiator.Peer && e.ReplyCode is 311 or 312 or 313 or 403 or 404 or 405 or 406;

        #endregion

        #region Init consume channel and processing incoming event

        private async Task Consumer_ReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
        {
            Interlocked.Increment(ref _inFlight);
            try
            {
                if (_stopping)
                {
                    // not taken: the channel's close gives it back to the queue
                    return;
                }
                // settled on the channel that delivered it: a delivery tag means nothing on another
                await HandleAsync(((AsyncDefaultBasicConsumer)sender).Channel, eventArgs);
            }
            finally
            {
                if (Interlocked.Decrement(ref _inFlight) == 0 && _stopping)
                {
                    _drained.TrySetResult();
                }
            }
        }

        private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs eventArgs)
        {
            var headers = eventArgs.BasicProperties.Headers ?? new Dictionary<string, object?>();
            var routingKey = headers.GetHeaderString("x-original-routing-key") ?? eventArgs.RoutingKey;


            var message = Encoding.UTF8.GetString(eventArgs.Body.Span);

            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("[{Queue}] Received event: {EventName} from {Broker}", _queueName, routingKey, eventArgs.Exchange);
                }

                var result = await ProcessingEventAsync(channel, eventArgs, routingKey, message);
                if (result == null)
                {
                    return;
                }
                if (result == EventDispatchResult.Success || result == EventDispatchResult.Duplicated || result == EventDispatchResult.InProgress)
                {
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                    return;
                }
                if (result == EventDispatchResult.NotHandled)
                {
                    // No handler found for event or processing failed and no retries are allowed
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("No handler processed RabbitMQ event: {EventName}", routingKey);
                    }
                }
                await RetryOrParkAsync(channel, eventArgs, routingKey, headers);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "----- ERROR Processing message \"{0}\". {1}", message, ex.Message);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace(ex.StackTrace);
                }
                // Left unsettled, the message would hold a prefetch slot until the channel closes, then come back.
                try
                {
                    if (channel.IsOpen)
                    {
                        await RetryOrParkAsync(channel, eventArgs, routingKey, headers);
                    }
                }
                catch (Exception settleEx)
                {
                    _logger.LogError(settleEx, "[{Queue}] Could not settle message {MessageId}; the broker redelivers it when the channel closes.",
                        _queueName, eventArgs.BasicProperties.MessageId);
                }
            }

        }

        /// <summary>
        /// A message that wasn't handled: the next retry tier, parking once they run out. Without a retry tier, it is rejected (a queue's
        /// <c>x-dead-letter-exchange</c> may bring it back), or parked once it has had its attempts (<see cref="RabbitMQConsumerBuilder.ParkAfter"/>).
        /// </summary>
        private async Task RetryOrParkAsync(IChannel channel, BasicDeliverEventArgs eventArgs, string routingKey, IDictionary<string, object?> headers)
        {
            var originalExchange = headers.GetHeaderString("x-original-exchange") ?? eventArgs.Exchange;
            var attempts = (headers.GetHeaderInt("x-attempts") ?? 0) + 1;

            var retryPolicy = _retryPolicyProvider == null ? null
                : await _retryPolicyProvider.GetRetryPolicyForSourceAsync(originalExchange, attempts);
            if (retryPolicy == null || (retryPolicy.IsMaxRetryReached && !retryPolicy.IsParkingEnabled))
            {
                var rejections = Rejections(headers, _queueName);
                if (_endpoint.ParkingExchange is { } parking && rejections + 1 >= _endpoint.ParkAfterAttempts)
                {
                    await SendToDeadLetterQueueAsync(channel, eventArgs, parking, $"MaxAttemptsReached_{rejections + 1}", mandatory: true);
                    return;
                }
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Max processing retries reached for event: {EventName}, attempts: {Attempts}. Send Nack.", routingKey, attempts);
                }
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
                return;
            }
            if (retryPolicy.IsMaxRetryReached && retryPolicy.IsParkingEnabled)
            {
                // Route to DLQ after max retries

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Max processing retries reached for event: {EventName}, attempts: {Attempts}. Send to DLX.", routingKey, attempts);
                }
                await SendToDeadLetterQueueAsync(channel, eventArgs, retryPolicy.Exchange, $"MaxRetriesReached_Attempts_{attempts}", mandatory: false);

                return;
            }

            await RetryAsync(channel, eventArgs, originalExchange, routingKey, retryPolicy, attempts);
        }

        /// <summary>
        /// How many times the broker has dead-lettered this message out of <paramref name="queue"/> for a rejection: its own record
        /// (<c>x-death</c>), which a queue routing rejected messages back to itself through a delay queue keeps adding to.
        /// </summary>
        internal static long Rejections(IDictionary<string, object?>? headers, string queue)
        {
            if (headers is null || !headers.TryGetValue("x-death", out var value) || value is not System.Collections.IEnumerable deaths || value is byte[])
            {
                return 0;
            }
            long total = 0;
            foreach (var death in deaths)
            {
                if (death is IDictionary<string, object?> entry
                    && entry.GetHeaderString("queue") == queue
                    && entry.GetHeaderString("reason") == "rejected")
                {
                    total += entry.TryGetValue("count", out var count) && count is IConvertible c ? c.ToInt64(null) : 1;
                }
            }
            return total;
        }

        /// <summary>A message that can't be read won't read better on a retry: parked if the policy or the endpoint allows, otherwise dropped.</summary>
        private async Task ParkUnreadableAsync(IChannel channel, BasicDeliverEventArgs eventArgs)
        {
            var source = eventArgs.BasicProperties.Headers.GetHeaderString("x-original-exchange") ?? eventArgs.Exchange;
            var retryPolicy = _retryPolicyProvider == null ? null
                : await _retryPolicyProvider.GetRetryPolicyForSourceAsync(source, int.MaxValue);
            if (retryPolicy is { IsParkingEnabled: true })
            {
                await SendToDeadLetterQueueAsync(channel, eventArgs, retryPolicy.Exchange, "DeserializationFailed", mandatory: false);
                return;
            }
            if (_endpoint.ParkingExchange is { } parking)
            {
                await SendToDeadLetterQueueAsync(channel, eventArgs, parking, "DeserializationFailed", mandatory: true);
                return;
            }
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
        }

        #endregion

        #region Processing

        /// <returns>What dispatching gave, or <c>null</c> when the message has been settled here.</returns>
        private async Task<EventDispatchResult?> ProcessingEventAsync(IChannel channel, BasicDeliverEventArgs eventArgs,
            string routingKey, string message)
        {
            using var _ = _logger.BeginScope($"Processing integration event: {routingKey}");
            var headers = eventArgs.BasicProperties.Headers;


            var eventType = await _subscriptionsManager.GetEventTypeByNameAsync(routingKey);
            if (eventType == null)
            {
                _logger.LogWarning("No event type found for event: {EventName}", routingKey);
                return EventDispatchResult.NotHandled;
            }
            IIntegrationEvent? integrationEvent = null;
            try
            {
                integrationEvent = _eventSerializer.Deserialize<IIntegrationEvent>(message, eventType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize message to {eventType}: {Message}", eventType.Name, ex.Message);
            }
            if (integrationEvent == null)
            {
                _logger.LogWarning("Failed to deserialize message to {eventType}", eventType.Name);
                await ParkUnreadableAsync(channel, eventArgs);
                return null;
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Processing RabbitMQ event: {EventName}, EventId: {EventId}.", routingKey, integrationEvent.MessageId);
            }

            var correlationId = headers.GetHeaderString("x-correlation-id")
                ?? eventArgs.BasicProperties.CorrelationId
                ?? StringIdGenerator.Instance.GenerateUniqueId();
            var messageId = headers.GetHeaderString("x-message-id")
                ?? eventArgs.BasicProperties.MessageId
                ?? integrationEvent.MessageId.ToString();

            var handlers = await _subscriptionsManager.GetHandlersForEventAsync(routingKey);

            var tenantId = eventArgs.BasicProperties.Headers?.GetHeaderString("x-tenant-id");
            // continues the trace carried by the message (traceparent header)
            var started = MessagingMetrics.StartTimestamp();
            using var activity = MessagingActivity.StartProcess("rabbitmq", _queueName, routingKey, messageId, headers, _queueName);
            try
            {
                MessageContext.Initialize(
                    correlationId: correlationId,
                    causationId: messageId,
                    executionId: StringIdGenerator.Instance.GenerateUniqueId(),
                    source: _serviceKey,
                    publisherId: Publisher(eventArgs.BasicProperties.UserId)
                    );
                MessagingActivity.EnrichWithMessageContext(activity);

                var result = await _dispatcher.DispatchAsync(integrationEvent, new EventDispatchContext(handlers, routingKey, tenantId, _serviceKey));
                MessagingActivity.SetResult(activity, result);
                MessagingMetrics.RecordConsumed("rabbitmq", _queueName, routingKey, result, started);
                return result;
            }
            catch (Exception ex)
            {
                MessagingActivity.SetError(activity, ex);
                MessagingMetrics.RecordConsumeError("rabbitmq", _queueName, routingKey, ex, started);
                throw;
            }
            finally
            {
                MessageContext.Clear();
            }
        }

        /// <summary>
        /// Who published the message, as the broker vouches: its <c>user_id</c>, which RabbitMQ only accepts from a connection signed in as
        /// that user (producers opt in with <c>StampUserId</c>). Null without one, as on the copies this engine republishes for retries and
        /// parking, which carry no <c>user_id</c>. A message the broker dead-letters itself (a queue's <c>x-dead-letter-exchange</c>)
        /// keeps its <c>user_id</c>.
        /// </summary>
        internal static string? Publisher(string? userId) => string.IsNullOrEmpty(userId) ? null : userId;

        private async Task RetryAsync(IChannel channel, BasicDeliverEventArgs eventArgs,
            string originalExchange, string originalRoutingKey,
            RetryPolicy retryPolicy, int attempts)
        {
            var headers = eventArgs.BasicProperties.Headers ?? new Dictionary<string, object?>();

            headers["x-attempts"] = attempts;
            headers["x-original-exchange"] = originalExchange;
            headers["x-original-routing-key"] = originalRoutingKey;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Retrying event: {EventName}, attempts: {Attempts}, exchange: {Exchange}, route: {Route}",
                    originalRoutingKey, headers["x-attempts"], retryPolicy.Exchange, retryPolicy.RoutingKey);
            }
            // re-publish with updated header
            await MoveAsync(channel, eventArgs, retryPolicy.Exchange, retryPolicy.RoutingKey, mandatory: true, headers);
        }

        /// <summary>
        /// Publishes a copy of the message (retry tier or parking) and acks the original only once the broker has
        /// confirmed the copy. When the copy is refused, the original is requeued instead of lost.
        /// </summary>
        private async Task MoveAsync(IChannel channel, BasicDeliverEventArgs eventArgs, string exchange, string routingKey, bool mandatory,
            IDictionary<string, object?> headers)
        {
            try
            {
                await channel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: mandatory,
                    basicProperties: new BasicProperties
                    {
                        ContentType = eventArgs.BasicProperties.ContentType,
                        CorrelationId = eventArgs.BasicProperties.CorrelationId,
                        MessageId = eventArgs.BasicProperties.MessageId,
                        Timestamp = eventArgs.BasicProperties.Timestamp,
                        DeliveryMode = DeliveryModes.Persistent,
                        Headers = headers
                    },
                    body: eventArgs.Body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Queue}] Could not move message {MessageId} to {Exchange} with routing key {RoutingKey}; it stays on the queue",
                    _queueName, eventArgs.BasicProperties.MessageId, exchange, routingKey);
                // a closed channel gives its unacked messages back to the queue by itself
                if (channel.IsOpen)
                {
                    // the same topology error would fail again at once: do not spin on it
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
                }
                return;
            }
            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        }

        // mandatory: true where nothing else would keep the message (endpoint parking), so a copy no queue takes is refused and the
        // original stays.
        private async Task SendToDeadLetterQueueAsync(IChannel channel, BasicDeliverEventArgs eventArgs, string exchange, string reason, bool mandatory)
        {
            var headers = eventArgs.BasicProperties.Headers ?? new Dictionary<string, object?>();
            headers["x-death-reason"] = reason;
            headers["x-death-timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            headers["x-original-queue"] = _queueName;

            var originalRoutingKey = headers.GetHeaderString("x-original-routing-key") ?? eventArgs.RoutingKey;
            var routingKey = GetDeadLetterRoutingKey(originalRoutingKey);
            _logger.LogWarning(
                "Sending message to DLQ. Queue: {Queue}, Original: {Event}, Routing: {Routing}, Reason: {Reason}",
                _queueName,
                originalRoutingKey, routingKey,
                reason);

            await MoveAsync(channel, eventArgs, exchange, routingKey, mandatory, headers);
        }

        private string GetDeadLetterRoutingKey(string originalRoutingKey)
        {
            return string.Format(_dlRoutingPattern, originalRoutingKey);
        }
        #endregion

    }
}
