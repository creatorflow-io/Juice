using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Juice.EventBus;
using Juice.Integrations.EventBus;
using Juice.MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.MediatR.Behaviors
{
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

                    return await next.Invoke(request, cancellationToken);
                }
                using var _ = _logger.BeginScope($"Exec Command: {typeName}");

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("----- Command data {CommandName} ({@Command})", typeName, request);
                }

                var response = await next.Invoke(request, cancellationToken);
                _?.Dispose();

                var transactionId = await ResilientTransaction.New(_dbContext, _logger).ExecuteAsync(async (transaction) =>
                {
                    _dbContext.BeginManageTransaction(transaction.TransactionId);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await _mediator.DispatchDomainEventsAsync(_dbContext, false);
                    await _mediator.DispatchAuditEventsAsync(_dbContext as IAuditableDbContext, false, _logger);
                    await _mediator.DispatchDataChangeEventsAsync(_dbContext as IAuditableDbContext, false, _logger);
                    await _integrationEventService.SaveEventsAsync(transaction.TransactionId);

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

                if (PublishIntegrationEvents && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ERROR Publishing integration events for transaction {TransactionId}", transactionId);
                    }
                }

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
