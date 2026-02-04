using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ
{
    public interface IRabbitMQPersistentConnection
        : IDisposable
    {
        string Name { get; }
        bool IsConnected { get; }

        ValueTask<bool> TryConnectAsync(CancellationToken cancellationToken = default);

        ValueTask DisconnectAsync();

        ValueTask<IChannel?> CreateChannelAsync(CancellationToken cancellationToken = default);
    }
}
