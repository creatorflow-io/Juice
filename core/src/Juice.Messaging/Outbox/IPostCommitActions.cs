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
        /// </summary>
        void Flush();
    }

    internal sealed class PostCommitActions : IPostCommitActions
    {
        private readonly List<Action> _actions = [];

        public void Add(Action action) => _actions.Add(action);

        public void Flush()
        {
            foreach (var action in _actions)
            {
                action();
            }
            _actions.Clear();
        }
    }
}
