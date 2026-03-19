using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Juice.Extensions;
using Juice.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.MediatR.Behaviors
{
    /// <summary>
    /// ┌──────────────────────────────────────────────────────────────────────────┐
    /// │                        MEDIATR TRANSACTION FLOW                          │
    /// │      (TransactionBehavior + Handler + UnitOfWork + Outbox + Publish)     │
    /// └──────────────────────────────────────────────────────────────────────────┘
    ///
    /// ┌───────────────┐
    /// │   MediatR     │
    /// │ Send(Command) │
    /// └───────┬───────┘
    ///         │
    ///         v
    /// ┌──────────────────────────────┐
    /// │ TransactionBehavior          │
    /// │ (Pipeline Behavior)          │
    /// └──────────────┬───────────────┘
    ///                │
    ///                │ HasActiveTransaction ?
    ///                ├───────────────────────────────────────────────────────────┐
    ///                │ Yes                                                       │
    ///                │   -> just call next() and return response                 │
    ///                │      the Transaction is managed by outer behavior         │
    ///                │ No                                                        
    ///                v                                                           
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 1) UnitOfWork BeginManage() notice context will be managed by behavior  │
    /// └─────────────────────────────────────────────────────────────────────────┘
    ///                |
    ///                v
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 2) next() executes Command Handler (BUSINESS OUTSIDE TRANSACTION)       │
    /// │    - Validate / Compute                                                 │
    /// │    - Change Aggregate                                                   │
    /// │    - Raise Domain Events                                                │
    /// └─────────────────────────────────────────────────────────────────────────┘
    ///                │
    ///                v
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 3) Begin TransactionContext via ExecutionStrategy / ResilientTransaction│
    /// │    - UnitOfWork(DbContext#1) BeginTransaction()                         │
    /// └─────────────────────────────────────────────────────────────────────────┘
    ///                │
    ///                v
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 5) SaveChanges #1 (Persist Domain Entity)                               │
    /// │    - INSERT Content                                                     │
    /// │    - Generated IDs ready (for audit/data events)                        │
    /// └─────────────────────────────────────────────────────────────────────────┘
    ///                │
    ///                v
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 6) Dispatch Events (in-process)                                         │
    /// │    - DispatchDomainEvents                                               │
    /// │    - DispatchAuditEvents                                                │
    /// │    - DispatchDataChangeEvents                                           │
    /// └─────────────────────────────────────────────────────────────────────────┘
    ///                │
    ///                v
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 7) Save Outbox in the same DbContext as IOutboxContext                  │
    /// |    or using DbContext#2 (IntegrationEventLog)                           │
    /// │    - DbContext#2 uses SAME DbConnection, DbTransaction as DbContext#1   │
    /// └─────────────────────────────────────────────────────────────────────────┘
    ///                │
    ///                v
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 8) CommitTransaction(txId)                                              │
    /// │    - commit only after outbox persisted                                 │
    /// │    - ClearEvents()                                                      │
    /// └─────────────────────────────────────────────────────────────────────────┘
    ///                │
    ///                v
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 9) Publish Integration Events (AFTER COMMIT)                            │
    /// │    - Query Outbox by TransactionId + State=NotPublished                 │
    /// │    - Mark InProgress                                                    │
    /// │    - Publish to EventBus (RabbitMQ)                                     │
    /// │    - Mark Published / Failed + update TimesSent / ModificationTime      │
    /// └─────────────────────────────────────────────────────────────────────────┘
    ///                │
    ///                v
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │ 10) Return Response (MediatR pipeline completes)                        │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    /// <typeparam name="TContext"></typeparam>
    public abstract class TransactionBehavior<TRequest, TResponse, TContext>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TContext : DbContext, IUnitOfWork
    {
        public int Order => int.MaxValue - 20; // run late
        private readonly ILogger _logger;
        private readonly TContext _dbContext;
        private readonly IOutboxService _outboxService;
        private readonly IMediator _mediator;
        private readonly IPostCommitActions? _postCommitActions;
        /// <summary>
        /// Gets a value indicating whether integration events should be published after processing.
        /// </summary>
        /// <remarks>Override this property in a derived class to change the default behavior of
        /// publishing integration events.</remarks>
        protected virtual bool PublishIntegrationEvents => true;

        public TransactionBehavior(TContext dbContext,
            IOutboxService<TContext> integrationEventService,
            IMediator mediator,
            ILogger logger,
            IPostCommitActions? postCommitActions = null)
        {
            _dbContext = dbContext ?? throw new ArgumentException(typeof(TContext).Name);
            _outboxService = integrationEventService ?? throw new ArgumentException(nameof(integrationEventService));
            _mediator = mediator ?? throw new ArgumentException(nameof(IMediator));
            _logger = logger ?? throw new ArgumentException(nameof(ILogger));
            _postCommitActions = postCommitActions;
        }

        public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            var typeName = request.GetGenericTypeName();

            try
            {
                if (_dbContext.HasActiveTransaction)
                {
                    _logger.LogDebug("DbContext has active transaction");
                    // The transaction is managed by outer behavior
                    return await next.Invoke(request, cancellationToken);
                }
                using var _ = _logger.BeginScope($"Exec Command: {typeName}");

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("----- Command data {CommandName} ({@Command})", typeName, request);
                }

                // Notice DbContext will be managed by behavior
                _dbContext.BeginManage();

                var response = await next.Invoke(request, cancellationToken);
                _?.Dispose();

                var committed = false;
                var transactionId = await ResilientTransaction.New(_dbContext, _logger).ExecuteAsync(async (transaction) =>
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await _mediator.DispatchDomainEventsAsync(_dbContext, false);
                    await _mediator.DispatchAuditEventsAsync(_dbContext as IAuditableDbContext, false, _logger);
                    await _mediator.DispatchDataChangeEventsAsync(_dbContext as IAuditableDbContext, false, _logger);
                    await _outboxService.SaveEventsAsync(transaction.TransactionId, cancellationToken);

                    // commit transaction if needed
                    if (!_dbContext.HasActiveTransaction)
                    {
                        return;
                    }
                    if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
                    {
                        _logger.LogDebug("----- Committing transaction {TransactionId}", transaction.TransactionId);
                    }

                    await _dbContext.CommitTransactionAsync(transaction.TransactionId, cancellationToken);
                    committed = true;

                    if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
                    {
                        _logger.LogDebug("----- Transaction {TransactionId} committed", transaction.TransactionId);
                    }
                    _dbContext.ClearEvents();
                }, cancellationToken);

                // Flush deferred local dispatch actions (e.g., channel enqueue
                // for "local" routes) only after a successful commit.
                // MUST be outside ResilientTransaction — if Flush throws, we must
                // not retry the already-committed transaction.
                if (committed)
                {
                    if (_postCommitActions != null)
                        await _postCommitActions.FlushAsync(_logger);
                }
                else
                {
                    _postCommitActions?.Clear();
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR Handling transaction for {CommandName} ({@Command})", typeName, request);

                // Clear deferred actions from the failed transaction — they must
                // not fire since the domain data was rolled back.
                _postCommitActions?.Clear();

                throw;
            }
        }

    }
}
