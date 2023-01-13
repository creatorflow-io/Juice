using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers.Api.Behaviors.DependencyInjection
{
    public static class BehaviorServiceCollectionExtensions
    {
        public static IServiceCollection AddMediatRTimerBehaviors(this IServiceCollection services)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TimerTransactionBehavior<,>));
            return services;
        }

    }
}
