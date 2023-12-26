using System.Linq.Expressions;

namespace Juice.Domain
{
    /// <summary>
    /// We consider using <see cref="IUnitOfWork"/> insead of the repository pattern
    /// <para>OR only use the <see cref="IRepository{T}"/> if needed</para>
    /// </summary>
    public interface IUnitOfWork
    {
        bool HasActiveTransaction { get; }

        /// <summary>
        /// Commit transaction with specified id
        /// </summary>
        /// <param name="transactionId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Save changes to database
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// Add entity to context and save changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IOperationResult<T>> AddAndSaveAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class;

        /// <summary>
        /// Add entities to context and save changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entities"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IOperationResult> AddAndSaveAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)
            where T : class;

        /// <summary>
        /// Update entity in context and save changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IOperationResult> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class;

        /// <summary>
        /// Delete entity from context and save changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IOperationResult> DeleteAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class;

        /// <summary>
        /// Find entity by predicate
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class;

        /// <summary>
        /// Get queryable for entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IQueryable<T> Query<T>()
            where T : class;
    }

    public interface IUnitOfWork<out TAggregate> : IUnitOfWork
        where TAggregate : class
    {

    }
}
