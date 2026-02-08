using Juice.EventBus.Publishing;

namespace Juice.EventBus
{
    /// <summary>
    /// Event bus
    /// </summary>
    public interface IEventBus
    {
        ValueTask PublishAsync<T>(
            T @event, string? domain = default,
            CancellationToken ct = default)
            where T : IMessage;

        ValueTask PublishAsync<T>(
           T @event, string publisherKey, PublishContext context,
           CancellationToken ct = default)
           where T : IMessage;
    }
}
