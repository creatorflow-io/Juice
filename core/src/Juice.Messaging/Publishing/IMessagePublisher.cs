using Juice.Messaging.Context;

namespace Juice.Messaging.Publishing
{
    public interface IMessagePublisher
    {
        string Key { get; }
        /// <summary>
        /// Publish payload to implemented broker like RabbitMQ, Kafka
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        ValueTask PublishAsync(IMessage message, MessageContextData? context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish a message with optional outbox delivery IDs. When <paramref name="localDeliveryIds"/>
        /// is non-null, implementations that support in-process local delivery (e.g. the
        /// <c>"local-channel"</c> publisher) can forward the IDs so the dispatch handler can
        /// mark the corresponding outbox records as <c>Published</c> after successful dispatch.
        /// The default implementation ignores <paramref name="localDeliveryIds"/> and delegates
        /// to <see cref="PublishAsync(IMessage, MessageContextData?, CancellationToken)"/>.
        /// </summary>
        virtual ValueTask PublishAsync(IMessage message, MessageContextData? context, IReadOnlyList<Guid>? localDeliveryIds, CancellationToken cancellationToken = default)
            => PublishAsync(message, context, cancellationToken);
    }

}
