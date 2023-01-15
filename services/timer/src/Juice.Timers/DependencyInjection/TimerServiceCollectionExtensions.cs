using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers.DependencyInjection
{
    public static class TimerServiceCollectionExtensions
    {
        public static IServiceCollection AddTimerService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<TimerOptions>(configuration);
            services.AddTransient<ITimer, Timer>();
            services.AddSingleton<TimerManager>();
            return services;
        }
    }
}
