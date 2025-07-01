using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using Juice.Domain;
using Juice.EF.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.EF
{
    public abstract class RepositoryBase<T, TContext>(TContext context) : IRepository<T>
        where T : class
        where TContext : DbContext
    {
        private IUnitOfWork<T>? _unitOfWork = context is IUnitOfWork uow ? new UnitOfWorkWrapper<T>(uow) : default;
        public virtual IUnitOfWork<T> UnitOfWork => _unitOfWork ?? throw new InvalidOperationException("UnitOfWork is not initialized. Please ensure the DbContext implements IUnitOfWork.");
        protected virtual TContext DbContext { get; private set; } = context;

        public virtual Task<IOperationResult<T>> AddAsync(T entity, CancellationToken token = default)
            => DbContext.AddAndSaveInternalAsync(entity, token);
        public virtual Task<IOperationResult> DeleteAsync(T entity, CancellationToken token = default)
            => DbContext.DeleteInternalAsync(entity, token);
        public virtual Task<IOperationResult> UpdateAsync(T entity, CancellationToken token = default)
            => DbContext.UpdateInternalAsync(entity, token);
        public virtual Task<T?> FindAsync(Expression<Func<T, bool>> predicate, bool readOnly = false, CancellationToken token = default)
            => (readOnly ? DbContext.Set<T>().AsNoTracking() : DbContext.Set<T>()).FirstOrDefaultAsync(predicate, token);

        /// <summary>
        /// Create a predicate to find an entity by its key
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual Expression<Func<T, bool>> CreateIdPredicate<TKey>(TKey value)
        {
            var param = Expression.Parameter(typeof(T));
            MemberExpression? id = null;
            try
            {
                var name = typeof(T).GetProperties().Where(p => p.GetCustomAttribute<KeyAttribute>() != null).FirstOrDefault()?.Name;
                if (name != null)
                {
                    id = Expression.PropertyOrField(param, name);
                }
            }
            catch { }

            if (id == null && typeof(T).GetProperty("Id") != null)
            {
                id = Expression.PropertyOrField(param, "Id");
            }
            if (id == null && typeof(T).GetProperty($"{typeof(T).Name}Id") != null)
            {
                id = Expression.PropertyOrField(param, $"{typeof(T).Name}Id");
            }
            if (id == null)
            {
                throw new InvalidOperationException("Cannot find the key property of the entity");
            }
            var body = Expression.Equal(id, Expression.Constant(value));
            return Expression.Lambda<Func<T, bool>>(body, param);
        }
        public virtual Task<T?> ReadAsync<TKey>(TKey id, CancellationToken token = default)
            => FindAsync(CreateIdPredicate(id), true, token);
        public virtual Task<T?> GetAsync<TKey>(TKey id, CancellationToken token = default)
            => FindAsync(CreateIdPredicate(id), false, token);
        public virtual IQueryable<T> Query() => DbContext.Set<T>();
    }
}
