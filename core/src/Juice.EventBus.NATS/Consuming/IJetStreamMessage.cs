using NATS.Client.JetStream;

namespace Juice.EventBus.NATS.Consuming
{
    /// <summary>
    /// Seam over <see cref="INatsJSMsg{T}"/> so the consumer engine can be unit-tested without a server.
    /// </summary>
    internal interface IJetStreamMessage
    {
        string Subject { get; }
        ReadOnlyMemory<byte> Data { get; }
        /// <summary>
        /// Decoded headers (string values).
        /// </summary>
        IDictionary<string, object?> Headers { get; }
        /// <summary>
        /// Delivery count reported by the server, starting at 1.
        /// </summary>
        ulong NumDelivered { get; }

        ValueTask AckAsync(CancellationToken cancellationToken = default);
        ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken = default);
        ValueTask AckTerminateAsync(CancellationToken cancellationToken = default);
        ValueTask AckProgressAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class NatsJSMessageAdapter : IJetStreamMessage
    {
        private readonly INatsJSMsg<byte[]> _msg;

        public NatsJSMessageAdapter(INatsJSMsg<byte[]> msg)
        {
            _msg = msg;
            Headers = JetStreamHeaders.Decode(msg.Headers);
        }

        public string Subject => _msg.Subject;
        public ReadOnlyMemory<byte> Data => _msg.Data ?? Array.Empty<byte>();
        public IDictionary<string, object?> Headers { get; }
        public ulong NumDelivered => _msg.Metadata?.NumDelivered ?? 1;

        public ValueTask AckAsync(CancellationToken cancellationToken = default)
            => _msg.AckAsync(cancellationToken: cancellationToken);

        public ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken = default)
            => _msg.NakAsync(new AckOpts { NakDelay = delay }, cancellationToken);

        public ValueTask AckTerminateAsync(CancellationToken cancellationToken = default)
            => _msg.AckTerminateAsync(cancellationToken: cancellationToken);

        public ValueTask AckProgressAsync(CancellationToken cancellationToken = default)
            => _msg.AckProgressAsync(cancellationToken: cancellationToken);
    }
}
