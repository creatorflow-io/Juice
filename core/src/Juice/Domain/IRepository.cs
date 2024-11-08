using System.Linq.Expressions;

namespace Juice.Domain
{
    /// <summary>
    /// We consider using <see cref="IUnitOfWork"/> insead of the repository pattern
    /// <para>OR only use the <see cref="IRepository{T}"/> if needed</para>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IRepository<T>
        where T : class
    {
        IUnitOfWork<T> UnitOfWork { get; }

        Task<IOperationResult<T>> AddAsync(T entity, CancellationToken token = default);
        Task<IOperationResult> DeleteAsync(T entity, CancellationToken token = default);
        Task<IOperationResult> UpdateAsync(T entity, CancellationToken token = default);
        Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken token = default);
    }
}
