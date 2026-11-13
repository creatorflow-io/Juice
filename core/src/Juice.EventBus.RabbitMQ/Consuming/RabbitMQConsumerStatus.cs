namespace Juice.EventBus.RabbitMQ.Consuming
{
    /// <summary>Where a RabbitMQ consumer is (<see cref="RabbitMQConsumerStatus.State"/>).</summary>
    public enum RabbitMQConsumerState
    {
        /// <summary>Not consuming yet: the host is starting it, or it is retrying its start (the broker or the queue isn't there).</summary>
        Starting,
        /// <summary>The broker has registered the consumer: messages are delivered.</summary>
        Consuming,
        /// <summary>
        /// The consumer was lost: the connection dropped (the client recovers it), the broker closed its channel or cancelled it (the
        /// consumer starts again, with a backoff).
        /// </summary>
        Recovering,
        /// <summary>The host stopped it.</summary>
        Stopped,
    }

    /// <summary>
    /// The live state of one RabbitMQ consumer, resolved by its key: <c>GetRequiredKeyedService&lt;RabbitMQConsumerStatus&gt;(key)</c>.
    /// <see cref="Microsoft.Extensions.DependencyInjection.RabbitMQHealthCheckExtensions.AddRabbitMQConsumerHealthCheck"/> reads it. A
    /// connection can be up while its consumer is not (a queue deleted, a channel closed by the broker), so readiness that only checks
    /// the connection misses it.
    /// </summary>
    public sealed class RabbitMQConsumerStatus
    {
        private readonly object _lock = new();
        private RabbitMQConsumerState _state = RabbitMQConsumerState.Starting;
        private string? _reason;
        private DateTimeOffset _since = DateTimeOffset.UtcNow;

        public RabbitMQConsumerStatus(string key, string queue)
        {
            Key = key;
            Queue = queue;
        }

        public string Key { get; }

        public string Queue { get; }

        public RabbitMQConsumerState State { get { lock (_lock) { return _state; } } }

        /// <summary>Why it isn't consuming, when it isn't: the last error, or what the broker said.</summary>
        public string? Reason { get { lock (_lock) { return _reason; } } }

        /// <summary>When it entered <see cref="State"/> (or, while not consuming, when <see cref="Reason"/> last changed).</summary>
        public DateTimeOffset Since { get { lock (_lock) { return _since; } } }

        public bool IsConsuming => State == RabbitMQConsumerState.Consuming;

        /// <summary>The state, its reason and since when, read together.</summary>
        public (RabbitMQConsumerState State, string? Reason, DateTimeOffset Since) Snapshot()
        {
            lock (_lock)
            {
                return (_state, _reason, _since);
            }
        }

        internal void Set(RabbitMQConsumerState state, string? reason = null)
        {
            lock (_lock)
            {
                if (_state == RabbitMQConsumerState.Stopped)
                {
                    return; // late callbacks after a stop don't revive it
                }
                if (_state != state || _reason != reason)
                {
                    _since = DateTimeOffset.UtcNow;
                }
                _state = state;
                _reason = state == RabbitMQConsumerState.Consuming ? null : reason;
            }
        }

        internal void Stopped()
        {
            lock (_lock)
            {
                _state = RabbitMQConsumerState.Stopped;
                _reason = null;
                _since = DateTimeOffset.UtcNow;
            }
        }
    }
}
