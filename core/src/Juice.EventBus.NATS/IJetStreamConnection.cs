using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Juice.EventBus.NATS
{
    internal interface IJetStreamConnection
    {
        string Name { get; }
        INatsConnection Connection { get; }
        INatsJSContext JetStream { get; }
        NatsConnectionState State { get; }

        ValueTask EnsureConnectedAsync(CancellationToken cancellationToken = default);
    }
}
