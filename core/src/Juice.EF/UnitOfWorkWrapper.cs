using System.Linq.Expressions;
using Juice.Domain;

namespace Juice.EF
{
    internal class UnitOfWorkWrapper<TAggregate> : IUnitOfWork<TAggregate>
        where TAggregate : class
    {
        private IUnitOfWork _unitOfWork;
        public UnitOfWorkWrapper(Func<TAggregate?, IUnitOfWork> factory) =>
            _unitOfWork = factory(default);

        public bool HasActiveTransaction => _unitOfWork.HasActiveTransaction;

        public Task<IOperationResult<T>> AddAndSaveAsync<T>(T entity, CancellationToken token = default)
            where T : class
            => _unitOfWork.AddAndSaveAsync(entity, token);
        public Task<IOperationResult> AddAndSaveAsync<T>(IEnumerable<T> entities, CancellationToken token = default)
            where T : class
            => _unitOfWork.AddAndSaveAsync(entities, token);
        public Task<IOperationResult> DeleteAsync<T>(T entity, CancellationToken token = default)
            where T : class
            => _unitOfWork.DeleteAsync(entity, token);
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
            where T : class
            => _unitOfWork.FindAsync(predicate, token);
        public IQueryable<T> Query<T>()
            where T : class
            => _unitOfWork.Query<T>();
        public Task<IOperationResult> UpdateAsync<T>(T entity, CancellationToken token = default)
            where T : class
            => _unitOfWork.UpdateAsync(entity, token);

        public Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
            => _unitOfWork.CommitTransactionAsync(transactionId, token);
        public Task<int> SaveChangesAsync(CancellationToken token = default)
            => _unitOfWork.SaveChangesAsync(token);
    }
}
