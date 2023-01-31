namespace Juice.Timers.Behaviors
{
    internal class TimerManagerBehavior<T, R> : IPipelineBehavior<T, R?>
        where T : CreateTimerCommand, IRequest<R>
        where R : TimerRequest
    {
        private TimerManager _timer;
        private ILogger _logger;

        public TimerManagerBehavior(ILogger<TimerManagerBehavior<T, R>> logger, TimerManager timer)
        {
            _timer = timer;
            _logger = logger;
        }
        public async Task<R?> Handle(T request, CancellationToken cancellationToken, RequestHandlerDelegate<R?> next)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("---- Starting create timer ----. CorrelationId: " + request.CorrelationId);
            }
            var response = await next();

            if (response != null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("---- Ended timer creation ----. CorrelationId: " + response.CorrelationId + ". Start Timer Id: " + response.Id);
                }

                await _timer.StartAsync(response);
            }
            return response;
        }
    }
}
