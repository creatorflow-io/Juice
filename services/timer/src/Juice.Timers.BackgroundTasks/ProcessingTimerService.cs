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
            var processedIds = new List<Guid>();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TimerServiceOptions>>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();

                    var expiredTimerIds = await dbContext.TimerRequests
                        .AsNoTracking()
                        .Where(x => !_timer.ManagedIds.Contains(x.Id) && !processedIds.Contains(x.Id) && !x.IsCompleted && x.AbsoluteExpired < DateTimeOffset.Now)
                        .OrderBy(x => x.AbsoluteExpired)
                        //.Skip(count)
                        .Take(30)
                        .Select(x => x.Id)
                        .ToListAsync(stoppingToken);

                    if (expiredTimerIds.Any())
                    {
                        /// Try complete timer
                        count += expiredTimerIds.Count;
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

                        var notCompletedTimers = await dbContext.TimerRequests
                            .AsNoTracking()
                            .Where(x => notProcessedIds.Contains(x.Id) && !x.IsCompleted)
                            .OrderBy(x => x.AbsoluteExpired)
                            .Select(x => x.Id)
                            .ToListAsync(stoppingToken);

                        if (notCompletedTimers.Any())
                        {
                            await Task.Delay(2000);
                            notCompletedTimers = await dbContext.TimerRequests
                                .AsNoTracking()
                                .Where(x => notProcessedIds.Contains(x.Id) && !x.IsCompleted)
                                .OrderBy(x => x.AbsoluteExpired)
                                .Select(x => x.Id)
                                .ToListAsync(stoppingToken);
                            // Force complete timer
                            await Parallel.ForEachAsync(expiredTimerIds, async (expiredTimerId, token) =>
                            {
                                using var scope1 = _scopeFactory.CreateScope();
                                var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();
                                await mediator.Send(new CompleteTimerCommand(expiredTimerId));
                            });
                        }
                    }

                    if (!expiredTimerIds.Any())
                    {
                        count = 0;
                        processedIds.Clear();
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
