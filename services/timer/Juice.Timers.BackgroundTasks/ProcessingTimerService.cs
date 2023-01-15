using Juice.MediatR;
using Juice.Timers.Domain.Commands;
using Juice.Timers.EF;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Timers.BackgroundTasks
{
    internal class ProcessingTimerService : BackgroundService
    {
        private ILogger _logger;
        private IServiceScopeFactory _scopeFactory;
        private TimerManager _timer;

        public ProcessingTimerService(IServiceScopeFactory scopeFactory,
            ILogger<ProcessingTimerService> logger,
            TimerManager timer
            )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _timer = timer;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var count = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TimerServiceOptions>>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var expiredTimerIds = await dbContext.TimerRequests
                        .AsNoTracking()
                        .Where(x => !x.IsCompleted && x.AbsoluteExpired < DateTimeOffset.Now)
                        .OrderBy(x => x.AbsoluteExpired)
                        .Take(10)
                        .Select(x => x.Id)
                        .ToListAsync(stoppingToken);

                    if (expiredTimerIds.Any())
                    {
                        /// Try complete timer
                        foreach (var expiredTimerId in expiredTimerIds)
                        {
                            await mediator.Send(new IdentifiedCommand<CompleteTimerCommand, IOperationResult>(new CompleteTimerCommand(expiredTimerId), expiredTimerId));
                        }

                        var notCompletedTimers = await dbContext.TimerRequests
                            .AsNoTracking()
                            .Where(x => expiredTimerIds.Contains(x.Id) && !x.IsCompleted)
                            .OrderBy(x => x.AbsoluteExpired)
                            .Select(x => x.Id)
                            .ToListAsync(stoppingToken);

                        if (notCompletedTimers.Any())
                        {
                            await Task.Delay(1000);
                            notCompletedTimers = await dbContext.TimerRequests
                                .AsNoTracking()
                                .Where(x => expiredTimerIds.Contains(x.Id) && !x.IsCompleted)
                                .OrderBy(x => x.AbsoluteExpired)
                                .Select(x => x.Id)
                                .ToListAsync(stoppingToken);
                            // Force complete timer
                            foreach (var expiredTimerId in expiredTimerIds)
                            {
                                await mediator.Send(new CompleteTimerCommand(expiredTimerId));
                            }
                        }
                    }

                    if (!expiredTimerIds.Any())
                    {
                        count = 0;

                        var managedIds = _timer.ManagedIds;
                        if (managedIds.Length < 100)
                        {
                            var expiryBefore = DateTimeOffset.Now.Add(options.Value.MaxWaitTime);
                            var expirySoonTimers = await dbContext.TimerRequests
                                .AsNoTracking()
                                .Where(x => !managedIds.Contains(x.Id) && !x.IsCompleted && x.AbsoluteExpired < expiryBefore)
                                .OrderBy(x => x.AbsoluteExpired)
                                .Take(10)
                                .ToListAsync(stoppingToken);
                            foreach (var timer in expirySoonTimers)
                            {
                                await _timer.StartAsync(timer);
                            }
                        }
                        await Task.Delay(TimeSpan.FromSeconds(options.Value.ProcessingSecondsInterval));
                    }
                    else
                    {
                        count += expiredTimerIds.Count;
                    }
                }
                catch (TaskCanceledException)
                {

                }
                catch (Exception ex)
                {
                    _logger.LogError("An error occurred while processing timer. {Message}", ex.Message ?? "");
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }
            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogError("Shutting down...");
            }
        }
    }
}
