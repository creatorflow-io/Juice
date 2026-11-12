using System.Linq.Expressions;
using Juice.Domain;
using Juice.EF.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Juice.EF
{
    public abstract class UnitOfWork : DbContext, IUnitOfWork
    {
        public UnitOfWork(DbContextOptions options) : base(options)
        {
        }

        #region UnitOfWork

        private bool _isManaged = false;
        public virtual bool IsManaged => _isManaged;

        public virtual bool HasActiveTransaction
            => Database.CurrentTransaction != null
            && Database.CurrentTransaction.TransactionId != _commitedTransactionId;

        private Guid? _commitedTransactionId;

        public void BeginManage()
        {
            _isManaged = true;
        }

        public void EndManage()
        {
            _isManaged = false;
        }

        public virtual async Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
        {
            if (transactionId == _commitedTransactionId)
            {
                return false;
            }
            var transaction = Database.CurrentTransaction;
            if (transaction == null) { throw new InvalidOperationException("DbContext has not active transaction"); }

            if (transaction.TransactionId != transactionId) { throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current"); }

            try
            {
                await transaction.CommitAsync(token);
            }
            catch (Exception commitError)
            {
                await AbandonAsync(transaction, commitError);
                if (commitError is OperationCanceledException && token.IsCancellationRequested)
                {
                    throw;
                }
                // Never the original: a retrying execution strategy would re-run the transaction's work over changes SaveChanges already
                // accepted, commit nothing and report success. See TransactionCommitFailedException.
                throw new TransactionCommitFailedException(transaction.TransactionId, commitError);
            }

            _commitedTransactionId = transaction.TransactionId;
            try
            {
                await OnTransactionCommittedAsync();
            }
            catch (Exception ex)
            {
                // The data is committed, so a failing hook must not fail the command (callers would treat it as rolled back).
                CommitLogger?.LogWarning(ex, "Transaction {TransactionId} committed, but OnTransactionCommittedAsync failed: {Message}",
                    transaction.TransactionId, ex.Message);
            }
            return true;
        }

        /// <summary>
        /// Rolls back what may still be open after a failed commit, and detaches the transaction from the context. Nothing here may throw: the
        /// commit's error is the one callers act on. A provider may already have completed the transaction (Npgsql does when the server rejects
        /// COMMIT), and a broken connection fails the rollback too.
        /// </summary>
        private async Task AbandonAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, Exception commitError)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                // Usually "the transaction has completed": the provider ended it when COMMIT failed. Nothing is left open.
                CommitLogger?.LogDebug(ex, "Rolling back transaction {TransactionId} after its commit failed ({CommitError}): {Message}",
                    transaction.TransactionId, commitError.Message, ex.Message);
            }
            catch (Exception ex)
            {
                // Typically a broken connection: the transaction may have committed after all, or may still hold locks until the connection closes.
                CommitLogger?.LogWarning(ex, "Rolling back transaction {TransactionId} after its commit failed ({CommitError}) also failed; its outcome is unknown: {Message}",
                    transaction.TransactionId, commitError.Message, ex.Message);
            }
            try
            {
                // A failed rollback leaves the transaction current, so the context would still report it as active.
                await transaction.DisposeAsync();
            }
            catch (Exception ex)
            {
                CommitLogger?.LogDebug(ex, "Disposing transaction {TransactionId} after its commit failed also failed: {Message}",
                    transaction.TransactionId, ex.Message);
            }
            // EF marks a transaction disposed before disposing the provider's, so a throwing dispose skips detaching it and releasing the
            // connection that beginning it opened. Do both. (A connection the application opened itself stays open: it is the application's.)
            if (Database.CurrentTransaction?.TransactionId == transaction.TransactionId)
            {
                try
                {
                    Database.UseTransaction(null);
                    await Database.CloseConnectionAsync();
                }
                catch (Exception ex)
                {
                    CommitLogger?.LogWarning(ex, "Detaching transaction {TransactionId} after its commit failed also failed: {Message}",
                        transaction.TransactionId, ex.Message);
                }
            }
        }

        private ILogger? _commitLogger;
        private bool _commitLoggerResolved;

        /// <summary>
        /// The logger for commit problems that must not fail the command. By default from the context's logger factory, which is the
        /// application's when the context comes from dependency injection.
        /// </summary>
        protected virtual ILogger? CommitLogger
        {
            get
            {
                if (!_commitLoggerResolved)
                {
                    _commitLoggerResolved = true;
                    try
                    {
                        _commitLogger = this.GetService<ILoggerFactory>()?.CreateLogger(GetType());
                    }
                    catch (InvalidOperationException)
                    {
                        // No logger factory in the context's services.
                    }
                }
                return _commitLogger;
            }
        }

        /// <summary>Runs after a successful commit. A failure is logged and does not fail the commit, since the data is committed.</summary>
        protected virtual Task OnTransactionCommittedAsync() => Task.CompletedTask;

        async Task IUnitOfWork.AddAsync<T>(T entity, CancellationToken token)
            where T : class
        {
            await this.AddAsync(entity, token);
        }

        Task IUnitOfWork.AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken token)
            where T : class => this.AddRangeAsync(entities, token);

        public virtual Task<IOperationResult> DeleteAsync<T>(T entity, CancellationToken token = default)
            where T : class
            => this.DeleteInternalAsync(entity, token);

        public virtual Task<IOperationResult> UpdateAsync<T>(T entity, CancellationToken token = default)
            where T : class
            => this.UpdateInternalAsync(entity, token);

        public virtual async Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
            where T : class
        {
            return await Set<T>().FirstOrDefaultAsync(predicate, token);
        }

        public virtual IQueryable<T> Query<T>()
            where T : class
            => Set<T>();
        #endregion

        /// <summary>What belongs to one use of the context.</summary>
        protected void ResetUnitOfWorkState()
        {
            _isManaged = false;
            _commitedTransactionId = null;
        }

        // Before the base: disposing a pooled context hands it back, and it may be in use by the time the base returns.
        public override void Dispose()
        {
            ResetUnitOfWorkState();
            base.Dispose();
        }

        public override ValueTask DisposeAsync()
        {
            ResetUnitOfWorkState();
            return base.DisposeAsync();
        }
    }
}
