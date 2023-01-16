using Juice.Timers.Domain.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Timers.BackgroundTasks
{
    internal class CleanupTimerService : BackgroundService
    {
        private ILogger _logger;

        private IServiceScopeFactory _scopeFactory;
        public CleanupTimerService(
            IServiceScopeFactory scopeFactory,
            ILogger<CleanupTimerService> logger
            )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TimerServiceOptions>>();
                    var dateToCleanup = DateTimeOffset.Now.AddDays(-(options.Value.CleanupTimerAfterDays));

                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var cleanupResult = await mediator.Send(new CleanupTimersCommand(dateToCleanup));
                    if (!cleanupResult.Succeeded)
                    {
                        _logger.LogError("An error occurred while cleanup timers. {Message}", cleanupResult.ToString() ?? "");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(options.Value.CleanupMinutesInterval));
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
