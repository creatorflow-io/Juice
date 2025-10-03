using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ
{
    public interface IRabbitMQPersistentConnection
        : IDisposable
    {
        bool IsConnected { get; }

        ValueTask<bool> TryConnectAsync();

        ValueTask DisconnectAsync();

        ValueTask<IChannel?> CreateChannelAsync(CancellationToken cancellationToken = default);
    }
}
