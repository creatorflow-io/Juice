using System.Linq.Expressions;
using Juice.Domain;
using Microsoft.EntityFrameworkCore;

namespace Juice.EF
{
    public abstract class RepositoryBase<T, TContext>(TContext context) : IRepository<T>
        where T : class
        where TContext : DbContext, IUnitOfWork
    {
        public IUnitOfWork<T> UnitOfWork { get; private set; } = new UnitOfWorkWrapper<T>(context);
        protected TContext DbContext { get; private set; } = context;

        public virtual Task<IOperationResult<T>> AddAsync(T entity, CancellationToken token = default)
            => UnitOfWork.AddAndSaveAsync(entity, token);
        public virtual Task<IOperationResult> DeleteAsync(T entity, CancellationToken token = default)
            => UnitOfWork.DeleteAsync(entity, token);
        public virtual Task<IOperationResult> UpdateAsync(T entity, CancellationToken token = default)
            => UnitOfWork.UpdateAsync(entity, token);
        public virtual Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken token = default)
            => UnitOfWork.FindAsync(predicate, token);
    }
}
