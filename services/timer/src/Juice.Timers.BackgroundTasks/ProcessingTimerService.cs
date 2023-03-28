using Juice.MediatR;
using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;
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
        private IOptionsMonitor<TimerServiceOptions> _optionsMonitor;

        public ProcessingTimerService(IServiceScopeFactory scopeFactory,
            ILogger<ProcessingTimerService> logger,
            TimerManager timer,
            IOptionsMonitor<TimerServiceOptions> optionsMonitor
            )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _timer = timer;
            _optionsMonitor = optionsMonitor;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var processedIds = new List<Guid>();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var expiredTimerIds = await FindExpiredTimerIdsAsync(processedIds, stoppingToken);

                    if (expiredTimerIds.Any())
                    {
                        /// 
                        var notProcessedIds = new List<Guid>();
                        await Parallel.ForEachAsync(expiredTimerIds, async (expiredTimerId, token) =>
                        {
                            using var scope1 = _scopeFactory.CreateScope();
                            var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();

                            var rs = await mediator.Send(new IdentifiedCommand<CompleteTimerCommand>(new CompleteTimerCommand(expiredTimerId), expiredTimerId));
                            if (rs == null || !rs.Succeeded)
                            {
                                notProcessedIds.Add(expiredTimerId);
                            }
                            else
                            {
                                processedIds.Add(expiredTimerId);
                            }
                        });

                        var notCompletedTimers = await FindNotCompletedTimerIdsAsync(notProcessedIds, stoppingToken);

                        if (notCompletedTimers.Any())
                        {
                            await Task.Delay(2000);
                            notCompletedTimers = await FindNotCompletedTimerIdsAsync(notProcessedIds, stoppingToken);
                            // Force complete timer
                            await Parallel.ForEachAsync(notCompletedTimers, async (expiredTimerId, token) =>
                            {
                                using var scope1 = _scopeFactory.CreateScope();
                                var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();
                                await mediator.Send(new CompleteTimerCommand(expiredTimerId));
                            });
                        }
                    }
                    else
                    {
                        processedIds.Clear();
                        var managedIds = _timer.ManagedIds;
                        if (managedIds.Length < 100)
                        {
                            var expirySoonTimers = await FindExpirySoonIdsAsync(managedIds, stoppingToken);
                            foreach (var timer in expirySoonTimers)
                            {
                                await _timer.StartAsync(timer);
                            }
                        }

                        await Task.Delay(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.ProcessingSecondsInterval));
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

        private async Task<List<Guid>> FindExpiredTimerIdsAsync(List<Guid> excludeIds, CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();

            return await dbContext.TimerRequests
                 .AsNoTracking()
                 .Where(x => !_timer.ManagedIds.Contains(x.Id) && !excludeIds.Contains(x.Id) && !x.IsCompleted && x.AbsoluteExpired < DateTimeOffset.Now)
                 .OrderBy(x => x.AbsoluteExpired)
                 //.Skip(count)
                 .Take(30)
                 .Select(x => x.Id)
                 .ToListAsync(token);
        }

        private async Task<List<Guid>> FindNotCompletedTimerIdsAsync(List<Guid> ids, CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();

            return await dbContext.TimerRequests
                            .AsNoTracking()
                            .Where(x => ids.Contains(x.Id) && !x.IsCompleted)
                            .OrderBy(x => x.AbsoluteExpired)
                            .Select(x => x.Id)
                            .ToListAsync(token);
        }

        private async Task<List<TimerRequest>> FindExpirySoonIdsAsync(Guid[] excludeIds, CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();
            var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TimerServiceOptions>>();
            var expiryBefore = DateTimeOffset.Now.Add(options.Value.MaxWaitTime);
            return await dbContext.TimerRequests
                .AsNoTracking()
                .Where(x => !excludeIds.Contains(x.Id) && !x.IsCompleted && x.AbsoluteExpired < expiryBefore)
                .OrderBy(x => x.AbsoluteExpired)
                .Take(10)
                .ToListAsync(token);
        }
    }
}
