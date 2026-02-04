using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Juice.EventBus.Extensions;
using Juice.EventBus.Transactional;
using Juice.MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.MediatR.Behaviors
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
        private readonly IIntegrationEventService _integrationEventService;
        private readonly IMediator _mediator;
        /// <summary>
        /// Gets a value indicating whether integration events should be published after processing.
        /// </summary>
        /// <remarks>Override this property in a derived class to change the default behavior of
        /// publishing integration events.</remarks>
        protected virtual bool PublishIntegrationEvents => true;

        public TransactionBehavior(TContext dbContext,
            IIntegrationEventService<TContext> integrationEventService,
            IMediator mediator,
            ILogger logger)
        {
            _dbContext = dbContext ?? throw new ArgumentException(typeof(TContext).Name);
            _integrationEventService = integrationEventService ?? throw new ArgumentException(nameof(integrationEventService));
            _mediator = mediator ?? throw new ArgumentException(nameof(IMediator));
            _logger = logger ?? throw new ArgumentException(nameof(ILogger));
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

                var transactionId = await ResilientTransaction.New(_dbContext, _logger).ExecuteAsync(async (transaction) =>
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await _mediator.DispatchDomainEventsAsync(_dbContext, false);
                    await _mediator.DispatchAuditEventsAsync(_dbContext as IAuditableDbContext, false, _logger);
                    await _mediator.DispatchDataChangeEventsAsync(_dbContext as IAuditableDbContext, false, _logger);
                    await _integrationEventService.SaveEventsAsync(transaction.TransactionId, cancellationToken);

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

                    if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
                    {
                        _logger.LogDebug("----- Transaction {TransactionId} committed", transaction.TransactionId);
                    }
                    _dbContext.ClearEvents();
                }, cancellationToken);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR Handling transaction for {CommandName} ({@Command})", typeName, request);

                throw;
            }
        }

    }
}
