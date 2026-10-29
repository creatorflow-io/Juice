using Juice.EventBus.Publishing;
using Juice.Messaging.Diagnostics;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;

namespace Juice.EventBus.NATS.Publishing
{
    internal sealed class JetStreamProducer : ITransportPublisher, IAsyncDisposable, IDisposable
    {
        public string Key { get; init; }
        private readonly IJetStreamConnection _connection;
        private readonly JetStreamProducerEndpoint _endpoint;
        private readonly ILogger _logger;

        private volatile bool _disposedValue;
        public JetStreamProducer(
            ILoggerFactory loggerFactory,
            IJetStreamConnection connection,
            JetStreamProducerEndpoint endpoint
            )
        {
            Key = endpoint.Key;
            _connection = connection;
            _endpoint = endpoint;
            _logger = loggerFactory.CreateLogger(typeof(JetStreamProducer).FullName + $"[{endpoint.Key}]");
        }

        #region Publish outgoing event

        public async ValueTask PublishAsync(byte[] payload, PublishContext context,
            CancellationToken cancellationToken = default)
        {
            if (_disposedValue)
                throw new ObjectDisposedException(nameof(JetStreamProducer));

            var destination = context.Destination ?? _endpoint.DefaultDestination;
            var routingKey = JetStreamNaming.ResolveRoutingKey(context);
            JetStreamNaming.ValidateToken(destination, nameof(context.Destination));
            JetStreamNaming.ValidateToken(routingKey, nameof(context.RoutingKey));
            var subject = JetStreamNaming.BuildSubject(destination, routingKey);
            var messageId = context.MessageId;

            var headers = context.Headers != null
                ? new Dictionary<string, object?>(context.Headers)
                : new Dictionary<string, object?>();
            headers["x-original-exchange"] = destination;
            headers["x-original-routing-key"] = routingKey;
            if (!headers.ContainsKey("x-message-id"))
            {
                headers["x-message-id"] = messageId;
            }
            if (!headers.ContainsKey("x-tenant-id") && !string.IsNullOrEmpty(context.TenantId))
            {
                headers["x-tenant-id"] = context.TenantId;
            }

            using var activity = MessagingActivity.StartSend("nats", destination, routingKey, messageId);
            MessagingActivity.Inject(headers, activity);

            try
            {
                await _connection.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Publishing event to {Subject}: {EventId} {EventName}",
                        subject, messageId, routingKey);
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_endpoint.PublishTimeout);

                var ack = await _connection.JetStream.PublishAsync(
                    subject,
                    payload,
                    // per subject: one event routed to two subjects of the same stream is two messages
                    opts: new NatsJSPubOpts { MsgId = $"{messageId}:{subject}", RetryAttempts = _endpoint.MaxRetryAttempts },
                    headers: JetStreamHeaders.Encode(headers),
                    cancellationToken: cts.Token).ConfigureAwait(false);

                // A duplicate ack means the stream already holds this message id (e.g. the outbox
                // retried after a lost ack). That is a success; EnsureSuccess would throw for it.
                if (ack.Error == null && ack.Duplicate)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Event {EventId} was already stored in {Stream}; duplicate ignored.", messageId, ack.Stream);
                    }
                    MessagingMetrics.RecordPublished("nats", destination, routingKey);
                    return;
                }

                ack.EnsureSuccess();
                MessagingMetrics.RecordPublished("nats", destination, routingKey);
            }
            catch (NatsJSPublishNoResponseException ex)
            {
                MessagingActivity.SetError(activity, ex);
                MessagingMetrics.RecordPublishError("nats", destination, routingKey, ex);
                JetStreamMetrics.IncrementPublishError(subject);
                _logger.LogWarning(ex, "Could not publish event: {EventId} to {Subject}. No stream acknowledged it.", messageId, subject);
                throw new InvalidOperationException(
                    $"No JetStream stream acknowledged subject '{subject}' (destination '{destination}'): no stream captures the subject, or the server did not answer in time.", ex);
            }
            catch (Exception ex)
            {
                MessagingActivity.SetError(activity, ex);
                MessagingMetrics.RecordPublishError("nats", destination, routingKey, ex);
                JetStreamMetrics.IncrementPublishError(subject);
                _logger.LogWarning(ex, "Could not publish event: {EventId} to {Subject} ({ExceptionMessage})", messageId, subject, ex.Message);
                throw;
            }
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }

        public void Dispose()
        {
            // the connection is a shared keyed singleton, owned by the container
            _disposedValue = true;
        }
    }
}
