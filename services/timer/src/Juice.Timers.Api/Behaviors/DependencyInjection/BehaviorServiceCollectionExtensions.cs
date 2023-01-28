using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers.Api.Behaviors.DependencyInjection
{
    public static class BehaviorServiceCollectionExtensions
    {
        public static IServiceCollection AddMediatRTimerBehaviors(this IServiceCollection services)
        {
            services.AddScoped(typeof(IPipelineBehavior<CreateTimerCommand, TimerRequest>), typeof(TimerManagerBehavior<CreateTimerCommand, TimerRequest>));
            services.AddScoped(typeof(IPipelineBehavior<CreateTimerCommand, TimerRequest>), typeof(TimerTransactionBehavior<CreateTimerCommand, TimerRequest>));
            services.AddScoped(typeof(IPipelineBehavior<CompleteTimerCommand, IOperationResult>), typeof(TimerTransactionBehavior<CompleteTimerCommand, IOperationResult>));
            return services;
        }

    }
}
