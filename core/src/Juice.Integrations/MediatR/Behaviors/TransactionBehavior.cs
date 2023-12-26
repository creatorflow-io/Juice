using Juice.Domain;
using Juice.EF;
using Juice.EventBus;
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
        private readonly IIntegrationEventService<TContext> _integrationEventService;

        public TransactionBehavior(TContext dbContext,
            IIntegrationEventService<TContext> integrationEventService,
            ILogger logger)
        {
            _dbContext = dbContext ?? throw new ArgumentException(typeof(TContext).Name);
            _integrationEventService = integrationEventService ?? throw new ArgumentException(nameof(integrationEventService));
            _logger = logger ?? throw new ArgumentException(nameof(ILogger));
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var typeName = request.GetGenericTypeName();

            try
            {
                if (_dbContext.HasActiveTransaction)
                {
                    _logger.LogDebug("DbContext has active transaction");

                    return await next();
                }
                TResponse? response = default;

                Guid transactionId = await ResilientTransaction.New(_dbContext, _logger).ExecuteAsync(async (transaction) =>
                {
                    using (_logger.BeginScope($"Exec Command: {typeName}"))
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("----- Command data {CommandName} ({@Command})", typeName, request);
                        }
                        response = await next();
                    }
                }, cancellationToken);
                await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId);

                return response!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR Handling transaction for {CommandName} ({@Command})", typeName, request);

                throw;
            }
        }

    }
}
