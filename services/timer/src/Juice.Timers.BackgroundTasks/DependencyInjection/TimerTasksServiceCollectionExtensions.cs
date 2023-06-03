using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers.BackgroundTasks
{
    public static class TimerTasksServiceCollectionExtensions
    {
        public static IServiceCollection AddTimerBackgroundTasks(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<TimerServiceOptions>(configuration);
            services.AddHostedService<CleanupTimerService>();
            services.AddHostedService<ProcessingTimerService>();
            return services;
        }
    }
}
