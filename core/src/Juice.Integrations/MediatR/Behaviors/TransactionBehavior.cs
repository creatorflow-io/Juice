using Juice.EF;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.Integrations.EventBus;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.MediatR.Behaviors
{
    public abstract class TransactionBehavior<TRequest, TResponse, TContext>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TContext : DbContext, IUnitOfWork
    {
        private readonly ILogger _logger;
        private readonly TContext _dbContext;
        private readonly IIntegrationEventService _integrationEventService;

        public TransactionBehavior(TContext dbContext,
            IIntegrationEventService<TContext> integrationEventService,
            ILogger logger)
        {
            _dbContext = dbContext ?? throw new ArgumentException(typeof(TContext).Name);
            _integrationEventService = integrationEventService ?? throw new ArgumentException(nameof(integrationEventService));
            _logger = logger ?? throw new ArgumentException(nameof(ILogger));
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            var response = default(TResponse);
            var typeName = request.GetGenericTypeName();

            try
            {
                _logger.LogInformation("Transaction behavior");
                if (_dbContext.HasActiveTransaction)
                {
                    return await next();
                }
                await ResilientTransaction.New(_dbContext).ExecuteAsync(async (transaction) =>
                {
                    // Achieving atomicity between original catalog database operation and the IntegrationEventLog thanks to a local transaction
                    using (_logger.BeginScope(CreateLogScope(transaction.TransactionId)))
                    {
                        _logger.LogInformation("----- Begin transaction {TransactionId} for {CommandName} ({@Command})", transaction.TransactionId, typeName, request);

                        response = await next();

                        _logger.LogInformation("----- Commit transaction {TransactionId} for {CommandName}", transaction.TransactionId, typeName);

                        await _dbContext.CommitTransactionAsync(transaction);
                    }

                    await _integrationEventService.PublishEventsThroughEventBusAsync(transaction.TransactionId);
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR Handling transaction for {CommandName} ({@Command})", typeName, request);

                throw;
            }
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
