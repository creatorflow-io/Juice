using System.Linq.Expressions;
using Juice.Domain;

namespace Juice.EF
{
    internal class UnitOfWorkWrapper<TAggregate> : IUnitOfWork<TAggregate>
        where TAggregate : class
    {
        private IUnitOfWork _unitOfWork;
        public UnitOfWorkWrapper(IUnitOfWork uow) =>
            _unitOfWork = uow;

        public bool HasActiveTransaction => _unitOfWork.HasActiveTransaction;

        public bool IsManaged => _unitOfWork.IsManaged;
        public Task AddAsync<T>(T entity, CancellationToken token = default)
            where T : class
            => _unitOfWork.AddAsync(entity, token);
        public Task AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken token = default)
            where T : class
            => _unitOfWork.AddRangeAsync(entities, token);
        public Task<IOperationResult> DeleteAsync<T>(T entity, CancellationToken token = default)
            where T : class
            => _unitOfWork.DeleteAsync(entity, token);
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
            where T : class
            => _unitOfWork.FindAsync(predicate, token);
        public IQueryable<T> Query<T>()
            where T : class
            => _unitOfWork.Query<T>();
     
        public Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
            => _unitOfWork.CommitTransactionAsync(transactionId, token);
        public Task<int> SaveChangesAsync(CancellationToken token = default)
            => _unitOfWork.SaveChangesAsync(token);
        public void BeginManage()
            => _unitOfWork.BeginManage();
    }
}
