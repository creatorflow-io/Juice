using System.Linq.Expressions;

namespace Juice.Domain
{
    public interface IRepository<T>
        where T : class
    {
        Task<IOperationResult<T>> AddAsync(T entity, CancellationToken token);
        Task<IOperationResult> UpdateAsync(T entity, CancellationToken token);
        Task<IOperationResult> DeleteAsync(T entity, CancellationToken token);

        Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken token);
    }
}
