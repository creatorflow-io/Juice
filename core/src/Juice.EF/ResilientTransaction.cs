using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.EF
{
    public class ResilientTransaction
    {
        private DbContext _context;
        private ResilientTransaction(DbContext context) =>
            _context = context ?? throw new ArgumentNullException(nameof(context));

        public static ResilientTransaction New(DbContext context) =>
            new ResilientTransaction(context);

        public async Task ExecuteAsync(Func<IDbContextTransaction, Task> action)
        {
            //Use of an EF Core resiliency strategy when using multiple DbContexts within an explicit BeginTransaction():
            //See: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                if (_context is IUnitOfWork unitOfWork)
                {

                    using var transaction = unitOfWork.HasActiveTransaction
                    ? unitOfWork.GetCurrentTransaction()
                    : await unitOfWork.BeginTransactionAsync();
                    await action(transaction);
                    await unitOfWork.CommitTransactionAsync(transaction);
                }
                else
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    await action(transaction);
                    transaction.Commit();
                }
            });
        }
    }
}
