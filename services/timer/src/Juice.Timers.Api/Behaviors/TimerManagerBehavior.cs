using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;
using Juice.Timers.EF;

namespace Juice.Timers.Api.Behaviors
{
    internal class TimerManagerBehavior<T, R> : IPipelineBehavior<T, R?>
        where T : CreateTimerCommand, IRequest<R>
        where R : TimerRequest
    {
        private TimerManager _timer;
        private ILogger _logger;
        private TimerDbContext _dbContext;

        public TimerManagerBehavior(ILogger<TimerManagerBehavior<T, R>> logger, TimerManager timer, TimerDbContext dbContext)
        {
            _timer = timer;
            _logger = logger;
            _dbContext = dbContext;
        }
        public async Task<R?> Handle(T request, CancellationToken cancellationToken, RequestHandlerDelegate<R?> next)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("---- Starting create timer ----. CorrelationId: " + request.CorrelationId);
                if (_dbContext.HasActiveTransaction)
                {
                    _logger.LogDebug("CorrelationId: {CorrelationId} Inside transaction {Transactionid}", request.CorrelationId,
                        _dbContext.GetCurrentTransaction()?.TransactionId);
                }
            }
            var response = await next();

            if (response != null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("---- Ended timer creation ----. CorrelationId: " + response.CorrelationId + ". Start Timer Id: " + response.Id);
                    if (_dbContext.HasActiveTransaction)
                    {
                        _logger.LogDebug("CorrelationId: {CorrelationId} still inside transaction {Transactionid}", request.CorrelationId,
                        _dbContext.GetCurrentTransaction()?.TransactionId);
                    }
                    else
                    {
                        _logger.LogDebug("CorrelationId: {CorrelationId} completed transaction", request.CorrelationId);
                    }
                }

                await _timer.StartAsync(response);
            }
            return response;
        }
    }
}
