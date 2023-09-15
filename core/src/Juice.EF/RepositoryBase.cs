using System.Linq.Expressions;
using Juice.Domain;
using Microsoft.EntityFrameworkCore;

namespace Juice.EF
{
    public abstract class RepositoryBase<T, TContext> : IRepository<T>
        where T : class
        where TContext : DbContext
    {
        protected readonly TContext Context;
        public RepositoryBase(TContext context) => Context = context;
        public virtual async Task<IOperationResult<T>> AddAsync(T entity, CancellationToken token)
        {
            try
            {
                var entry = Context.Set<T>().Add(entity);
                await Context.SaveChangesAsync(token);
                return OperationResult.Result(entry.Entity);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed<T>(ex);
            }
        }
        public virtual async Task<IOperationResult> DeleteAsync(T entity, CancellationToken token)
        {
            try
            {
                Context.Set<T>().Remove(entity);
                await Context.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public virtual async Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken token)
        {
            return await Context.Set<T>().FirstOrDefaultAsync(predicate, token);
        }
        public virtual async Task<IOperationResult> UpdateAsync(T entity, CancellationToken token)
        {
            try
            {
                var tracked = Context.Entry(entity).State != EntityState.Detached;
                if (!tracked)
                {
                    Context.Set<T>().Update(entity);
                }
                await Context.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
