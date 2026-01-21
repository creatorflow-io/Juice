using System.Linq.Expressions;
using Juice.Domain;
using Juice.EF.Extensions;
using Microsoft.EntityFrameworkCore;

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

        public override void Dispose()
        {
            base.Dispose();
            _commitedTransactionId = null;
        }
    }
}
