using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ
{
    public interface IRabbitMQPersistentConnection
        : IDisposable
    {
        string Name { get; }
        bool IsConnected { get; }

        /// <summary>
        /// The broker user this connection signs in as. RabbitMQ checks a message's <c>user_id</c> against it (validated user-id), so a
        /// publisher can only stamp its own. Null when unknown.
        /// </summary>
        string? UserName => null;

        ValueTask<bool> TryConnectAsync(CancellationToken cancellationToken = default);

        ValueTask DisconnectAsync();

        ValueTask<IChannel?> CreateChannelAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a channel with the given options, e.g. with publisher confirmations enabled.
        /// </summary>
        ValueTask<IChannel?> CreateChannelAsync(CreateChannelOptions? options, CancellationToken cancellationToken = default);
    }
}
