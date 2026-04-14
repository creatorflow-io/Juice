using System.Threading.Channels;
using Juice.Messaging.Context;
using Juice.Messaging.Publishing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Messaging.Local.Internal
{
    internal class LocalChannelMessagePublisher : IMessagePublisher
    {
        public string Key => "local-channel";

        private readonly ChannelWriter<ChannelEnvelope> _channelWriter;
        private readonly ChannelReader<ChannelEnvelope> _channelReader;
        private readonly ILogger _logger;

        // Cached at construction from IOptions (singleton — values never change at runtime).
        private readonly int? _capacity;
        private readonly int? _warningThreshold; // pre-computed: (int)(capacity * threshold)
        private readonly BoundedChannelFullMode _fullMode;
        private readonly bool _isBounded;

        public LocalChannelMessagePublisher(
            ChannelWriter<ChannelEnvelope> channelWriter,
            ChannelReader<ChannelEnvelope> channelReader,
            IOptions<LocalChannelOptions> options,
            ILogger<LocalChannelMessagePublisher> logger)
        {
            _channelWriter = channelWriter;
            _channelReader = channelReader;
            _logger = logger;

            var opts = options.Value;
            _capacity = opts.Capacity is > 0 ? opts.Capacity : null;
            _isBounded = _capacity.HasValue;
            _fullMode = opts.FullMode;
            _warningThreshold = _capacity.HasValue
                ? (int)(_capacity.Value * opts.FullWarningThreshold)
                : null;
        }

        public ValueTask PublishAsync(IMessage message, MessageContextData? context, IReadOnlyList<Guid>? localDeliveryIds, CancellationToken cancellationToken = default)
            => PublishAsync(message, context, cancellationToken, localDeliveryIds);

        public async ValueTask PublishAsync(IMessage message, MessageContextData? context, CancellationToken cancellationToken = default)
            => await PublishAsync(message, context, cancellationToken, localDeliveryIds: null);

        private async ValueTask PublishAsync(IMessage message, MessageContextData? context, CancellationToken cancellationToken, IReadOnlyList<Guid>? localDeliveryIds)
        {
            var envelope = new ChannelEnvelope(message, context, localDeliveryIds);

            if (_channelWriter.TryWrite(envelope))
            {
                // Write succeeded — warn if queue depth has crossed the pre-computed threshold.
                // For DropOldest/DropNewest modes this is the only signal that items may have
                // been silently evicted from the queue.
                if (_warningThreshold.HasValue)
                {
                    var depth = _channelReader.Count;
                    if (depth >= _warningThreshold.Value)
                    {
                        _logger.LogWarning(
                            "Local channel queue depth {Depth}/{Capacity} has reached the warning threshold (mode={FullMode})",
                            depth, _capacity!.Value, _fullMode);
                    }
                }
                return;
            }

            // TryWrite returned false.
            // DropOldest/DropNewest always return true (they evict to make room), so false
            // here only means the channel is closed — fall through to WriteAsync which throws.
            // DropWrite returns false when full (item silently discarded) or when closed.
            if (_fullMode == BoundedChannelFullMode.DropWrite)
            {
                // Channel is full and the incoming message was not queued.
                _logger.LogWarning(
                    "Local channel full (capacity={Capacity}, mode=DropWrite), {MessageType} (id={MessageId}) dropped",
                    _capacity!.Value, message.GetType().Name, message.MessageId);
                LocalChannelMetrics.IncrementChannelDropped(Key, message.GetType().Name);
            }
            else
            {
                // Wait mode (back-pressure), or channel closed — await; throws ChannelClosedException if closed.
                _logger.LogDebug(
                    "Local channel full (depth={Depth}/{Capacity}), waiting for capacity",
                    _channelReader.Count, _capacity?.ToString() ?? "∞");
                await _channelWriter.WriteAsync(envelope, cancellationToken);
            }
        }
    }
}
