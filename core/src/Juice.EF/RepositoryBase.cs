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
            var param = Expression.Parameter(typeof(T), "x");
            MemberExpression? id = null;

            // Try to find the key property by [Key] attribute, or fallback to "Id" or "{TypeName}Id"
            var keyProp = (typeof(T).GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null)
                ?? typeof(T).GetProperty("Id")
                ?? typeof(T).GetProperty($"{typeof(T).Name}Id")) ?? throw new InvalidOperationException("Cannot find the key property of the entity");

            id = Expression.Property(param, keyProp);

            // Convert the string value to the actual type of the key
            object convertedValue;
            try
            {
                var targetType = Nullable.GetUnderlyingType(keyProp.PropertyType) ?? keyProp.PropertyType;
                if (typeof(TKey) == targetType)
                {
                    // If TKey is not the same type as the key property, we need to convert it
                    convertedValue = value!;
                }
                else
                {
                    convertedValue = Convert.ChangeType(value, targetType)!;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert value '{value}' to type {keyProp.PropertyType}", ex);
            }

            var constant = Expression.Constant(convertedValue, keyProp.PropertyType);
            var body = Expression.Equal(id, constant);

            return Expression.Lambda<Func<T, bool>>(body, param);
        }
        /// <inheritdoc/>
        public virtual Task<T?> ReadAsync<TKey>(TKey id, CancellationToken token = default)
            => FindAsync(CreateIdPredicate(id), true, token);
        /// <inheritdoc/>
        public virtual Task<T?> GetAsync<TKey>(TKey id, CancellationToken token = default)
            => FindAsync(CreateIdPredicate(id), false, token);
        /// <inheritdoc/>
        public virtual Task<bool> ExistsAsync<TKey>(TKey id, CancellationToken token = default)
        {
            var predicate = CreateIdPredicate(id);
            return DbContext.Set<T>().AnyAsync(predicate, token);
        }
        /// <inheritdoc/>
        public virtual IQueryable<T> Query() => DbContext.Set<T>();
    }
}
