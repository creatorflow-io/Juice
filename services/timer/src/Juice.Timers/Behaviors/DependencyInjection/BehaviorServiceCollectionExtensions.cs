using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers.Behaviors.DependencyInjection
{
    public static class BehaviorServiceCollectionExtensions
    {
        /// <summary>
        /// Try to start timer after created
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddMediatRTimerManagerBehavior(this IServiceCollection services)
        {
            services.AddScoped(typeof(IPipelineBehavior<CreateTimerCommand, TimerRequest>), typeof(TimerManagerBehavior<CreateTimerCommand, TimerRequest>));
            return services;
        }

    }
}
