using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.EF
{
    public interface IUnitOfWork
    {
        IDbContextTransaction? GetCurrentTransaction();
        bool HasActiveTransaction { get; }

        Task<IDbContextTransaction?> BeginTransactionAsync();
        Task CommitTransactionAsync(IDbContextTransaction transaction);
        void RollbackTransaction();
    }
}
