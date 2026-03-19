using System.Linq.Expressions;

namespace Juice.Domain
{
    /// <summary>
    /// We consider using <see cref="IUnitOfWork"/> insead of the repository pattern
    /// <para>OR only use the <see cref="IRepository{T}"/> if needed</para>
    /// </summary>
    public interface IUnitOfWork: IManagable
    {
        /// <summary>
        /// Gets a value indicating whether there is an active transaction associated with the current context.
        /// </summary>
        bool HasActiveTransaction { get; }

        /// <summary>
        /// Begin manage context from outside
        /// </summary>
        void BeginManage();

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
        /// Add entity to the context (in memory)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task AddAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class;

        /// <summary>
        /// Add entities to the context (in memory)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entities"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)
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

    public interface IUnitOfWork<TAggregate> : IUnitOfWork
        where TAggregate : class
    {
        /// <summary>
        /// Get queryable for entity type <typeparamref name="TAggregate"/>
        /// </summary>
        /// <returns></returns>
        IQueryable<TAggregate> Query() => Query<TAggregate>();

        Task<TAggregate?> FindAsync(Expression<Func<TAggregate, bool>> predicate, CancellationToken cancellationToken = default)
            => FindAsync<TAggregate>(predicate, cancellationToken);

    }
}
