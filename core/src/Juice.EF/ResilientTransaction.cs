using Juice.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Juice.EF
{
    public class ResilientTransaction
    {
        private DbContext _context;
        private ILogger? _logger;
        private ResilientTransaction(DbContext context, ILogger? logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        public static ResilientTransaction New(DbContext context, ILogger? logger = default) =>
            new ResilientTransaction(context, logger);

        public async Task<Guid> ExecuteAsync(Func<IDbContextTransaction, Task> action, CancellationToken token = default)
        {
            //Use of an EF Core resiliency strategy when using multiple DbContexts within an explicit BeginTransaction():
            //See: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = _context.Database.CurrentTransaction ?? await _context.Database.BeginTransactionAsync();
                using (_logger?.BeginScope(CreateLogScope(transaction.TransactionId)))
                {
                    await action(transaction);

                    // commit transaction if needed
                    if (_context is IUnitOfWork unitOfWork)
                    {
                        if (unitOfWork.HasActiveTransaction)
                        {
                            if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
                            {
                                _logger.LogDebug("----- Committing transaction {TransactionId}", transaction.TransactionId);
                            }

                            await unitOfWork.CommitTransactionAsync(transaction.TransactionId, token);

                            if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
                            {
                                _logger.LogDebug("----- Transaction {TransactionId} committed", transaction.TransactionId);
                            }
                        }
                    }
                    else
                    {
                        if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
                        {
                            _logger.LogDebug("----- Committing transaction {TransactionId}", transaction.TransactionId);
                        }

                        await transaction.CommitAsync();

                        if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
                        {
                            _logger.LogDebug("----- Transaction {TransactionId} committed", transaction.TransactionId);
                        }
                    }

                }
                return transaction.TransactionId;
            });
        }

        protected virtual List<KeyValuePair<string, object>> CreateLogScope(object transactionId)
        {
            return new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("TransactionContext", transactionId)
            };
        }
    }
}
