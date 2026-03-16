using Juice.Messaging.Context;

namespace Juice.Messaging.Local.Internal
{
    /// <summary>
    /// Wraps an <see cref="IMessage"/> with an optional <see cref="MessageContextData"/>
    /// snapshot captured at enqueue time. This preserves the original <c>MessageContext</c>
    /// (correlation ID, source, etc.) across the channel boundary so the background
    /// service can restore it before dispatch — enabling correct idempotency keys.
    /// </summary>
    internal sealed record ChannelEnvelope(
        IMessage Message,
        MessageContextData? Context);
}
