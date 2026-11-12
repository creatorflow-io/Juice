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
        /// End managing context from outside
        /// </summary>
        void EndManage();

        /// <summary>
        /// Commit transaction with specified id
        /// </summary>
        /// <remarks>
        /// A failed commit throws (in Juice.EF, <c>TransactionCommitFailedException</c> with the provider's error inside it), and a cancelled one
        /// throws <see cref="OperationCanceledException"/>. Neither always means "not committed": when the connection broke or the request was
        /// cancelled while <c>COMMIT</c> was in flight, the transaction may have committed. Make a command safe to run twice (an idempotency key,
        /// a unique constraint) before running it again.
        /// </remarks>
        /// <param name="transactionId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>True when this call committed the transaction; false when it was already committed.</returns>
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
        /// <para>When <see cref="IManagable.IsManaged"/> (inside a command handled by a transaction behavior),
        /// the change is only staged and saved by the behavior together with the outbox.</para>
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
