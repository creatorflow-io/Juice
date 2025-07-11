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
        Task<T?> FindAsync(Expression<Func<T, bool>> predicate, bool readOnly = false, CancellationToken token = default);
        /// <summary>
        /// Read an entity by its key as not tracked object.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="id"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<T?> ReadAsync<TKey>(TKey id, CancellationToken token = default);
        /// <summary>
        /// Get an entity by its key as tracked object.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="id"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<T?> GetAsync<TKey>(TKey id, CancellationToken token = default);
        /// <summary>
        /// Check if an entity exists by its key.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="id"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync<TKey>(TKey id, CancellationToken token = default);
        /// <summary>
        /// Get a queryable for the entity type.
        /// </summary>
        /// <returns></returns>
        IQueryable<T> Query();
    }
}
