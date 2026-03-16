using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Outbox
{
    /// <summary>
    /// Scoped service that accumulates actions to execute after a managed transaction
    /// commits successfully. Used by <c>IMessageService&lt;TContext&gt;</c> to defer
    /// in-memory channel dispatch until after <c>TransactionBehavior</c> commits,
    /// enabling immediate local delivery without breaking transactional atomicity.
    /// </summary>
    public interface IPostCommitActions
    {
        /// <summary>
        /// Registers an action to execute after the transaction commits.
        /// </summary>
        void Add(Action action);

        /// <summary>
        /// Executes and clears all registered actions. Called by
        /// <c>TransactionBehavior</c> after <c>CommitTransactionAsync</c>.
        /// Failures are logged but do not throw — the transaction is already committed.
        /// </summary>
        void Flush(ILogger? logger = null);

        /// <summary>
        /// Clears all registered actions without executing them. Called on
        /// transaction rollback to prevent stale actions from firing.
        /// </summary>
        void Clear();
    }

    internal sealed class PostCommitActions : IPostCommitActions
    {
        private readonly List<Action> _actions = [];

        public void Add(Action action) => _actions.Add(action);

        public void Clear() => _actions.Clear();

        public void Flush(ILogger? logger = null)
        {
            foreach (var action in _actions)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    // Best-effort: don't stop remaining actions or throw.
                    // The transaction is already committed — these are non-critical
                    // optimistic dispatches (e.g., channel enqueue for "local" routes).
                    // DeliveryHostedService will retry from the outbox if this fails.
                    logger?.LogWarning(ex,
                        "Post-commit action failed. The transaction was committed successfully. " +
                        "Outbox delivery will retry if needed. {Message}", ex.Message);
                }
            }
            _actions.Clear();
        }
    }
}
