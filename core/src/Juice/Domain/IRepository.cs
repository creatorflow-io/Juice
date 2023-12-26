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
        IUnitOfWork UnitOfWork { get; }
    }

    public static class RepositoryExtensions
    {
        public static async Task<IOperationResult<T>> AddAsync<T>(this IRepository<T> repository, T entity,
            CancellationToken token)
            where T : class
            => await repository.UnitOfWork.AddAndSaveAsync(entity, token);
        public static async Task<IOperationResult> DeleteAsync<T>(this IRepository<T> repository, T entity,
            CancellationToken token)
            where T : class
            => await repository.UnitOfWork.DeleteAsync(entity, token);
        public static async Task<T?> FindAsync<T>(this IRepository<T> repository,
            Expression<Func<T, bool>> predicate, CancellationToken token)
            where T : class
            => await repository.UnitOfWork.FindAsync(predicate, token);
        public static async Task<IOperationResult> UpdateAsync<T>(this IRepository<T> repository,
            T entity, CancellationToken token)
            where T : class
            => await repository.UnitOfWork.UpdateAsync(entity, token);
    }
}
