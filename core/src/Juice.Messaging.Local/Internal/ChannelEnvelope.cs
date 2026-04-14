using Juice.Messaging.Context;

namespace Juice.Messaging.Local.Internal
{
    /// <summary>
    /// Wraps an <see cref="IMessage"/> with an optional <see cref="MessageContextData"/>
    /// snapshot captured at enqueue time. This preserves the original <c>MessageContext</c>
    /// (correlation ID, source, etc.) across the channel boundary so the background
    /// service can restore it before dispatch — enabling correct idempotency keys.
    /// </summary>
    /// <param name="Message">The message to dispatch.</param>
    /// <param name="Context">Optional message context snapshot captured at enqueue time.</param>
    /// <param name="LocalDeliveryIds">
    /// Outbox delivery IDs for <c>"local"</c> route messages. When non-null,
    /// <see cref="LocalChannelBackgroundService"/> resolves <see cref="Juice.Messaging.Outbox.IOutboxRepository"/>
    /// and calls <c>MarkAsPublishedAsync</c> for each ID after a successful dispatch,
    /// preventing the background delivery processor from re-processing already-handled records.
    /// <c>null</c> for <c>"local-channel"</c> messages (no outbox backing).
    /// </param>
    internal sealed record ChannelEnvelope(
        IMessage Message,
        MessageContextData? Context,
        IReadOnlyList<Guid>? LocalDeliveryIds = null);
}
