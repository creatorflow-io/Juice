namespace Juice.Messaging
{
    /// <summary>
    /// Unified application-layer publishing interface for in-process (local-channel) dispatch.
    /// Accepts any <see cref="IMessage"/> — domain events (<c>INotification</c>) or integration
    /// events (<c>IIntegrationEvent</c>) — and routes to the <c>"local-channel"</c> in-memory
    /// channel. Routes that require outbox writes (<c>"local"</c> or broker) are ignored by this
    /// non-generic interface; use <see cref="IMessageService{TContext}"/> for those.
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// Publishes <paramref name="message"/> according to the resolved publishing policy.
        /// Returns to the caller immediately after enqueueing <c>local-channel</c> messages;
        /// handler execution occurs asynchronously on the background service.
        /// </summary>
        Task PublishAsync(IMessage message, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Unified application-layer publishing interface for all route types: <c>"local-channel"</c>
    /// (in-memory, zero DB), <c>"local"</c> (outbox-backed, durable), and broker routes.
    /// The target <typeparamref name="TContext"/> is used to detect an active ambient transaction;
    /// when inside a managed transaction, outbox writes participate atomically. When called outside
    /// a transaction, the outbox write is committed as a standalone operation immediately.
    /// </summary>
    /// <typeparam name="TContext">
    /// The <c>DbContext</c> type that owns the outbox tables. Must be registered in DI so that
    /// <see cref="IOutboxService{TContext}"/> can write outbox records to the correct database.
    /// </typeparam>
    public interface IMessageService<TContext> : IMessageService
        where TContext : class
    {
    }
}
