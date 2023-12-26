using System.Linq.Expressions;
using Juice.Domain;
using Microsoft.EntityFrameworkCore;

namespace Juice.EF
{
    public abstract class UnitOfWork : DbContext, IUnitOfWork
    {
        public UnitOfWork(DbContextOptions options) : base(options)
        {
        }

        #region UnitOfWork

        public bool HasActiveTransaction
            => Database.CurrentTransaction != null
            && Database.CurrentTransaction.TransactionId != _commitedTransactionId;

        private Guid? _commitedTransactionId;

        public async Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
        {
            if (transactionId == _commitedTransactionId)
            {
                return false;
            }
            var transaction = Database.CurrentTransaction;
            if (transaction == null) { throw new ArgumentNullException(nameof(transaction)); }

            if (transaction.TransactionId != transactionId) { throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current"); }

            try
            {
                await SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                await OnTransactionCommittedAsync();
                _commitedTransactionId = transaction.TransactionId;
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        protected virtual Task OnTransactionCommittedAsync() => Task.CompletedTask;

        public virtual async Task<IOperationResult<T>> AddAndSaveAsync<T>(T entity, CancellationToken token = default)
            where T : class
        {
            try
            {
                var entry = Set<T>().Add(entity);
                await SaveChangesAsync(token);
                return OperationResult.Result(entry.Entity);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed<T>(ex);
            }
        }

        public virtual async Task<IOperationResult> AddAndSaveAsync<T>(IEnumerable<T> entities, CancellationToken token = default)
            where T : class
        {
            try
            {
                Set<T>().AddRange(entities);
                await SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public virtual async Task<IOperationResult> DeleteAsync<T>(T entity, CancellationToken token = default)
            where T : class
        {
            try
            {
                Set<T>().Remove(entity);
                await SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public virtual async Task<IOperationResult> UpdateAsync<T>(T entity, CancellationToken token = default)
            where T : class
        {
            try
            {
                var tracked = Entry(entity).State != EntityState.Detached;
                if (!tracked)
                {
                    Set<T>().Update(entity);
                }
                await SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public virtual async Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
            where T : class
        {
            return await Set<T>().FirstOrDefaultAsync(predicate, token);
        }

        public IQueryable<T> Query<T>()
            where T : class
            => Set<T>();
        #endregion

        public override void Dispose()
        {
            base.Dispose();
            _commitedTransactionId = null;
        }
    }
}
