using Juice.Timers.Behaviors.DependencyInjection;
using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers.Api.Behaviors.DependencyInjection
{
    public static class BehaviorServiceCollectionExtensions
    {
        /// <summary>
        /// Included AddMediatRTimerManagerBehavior and TimerTransactionBehavior 
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddMediatRTimerBehaviors(this IServiceCollection services)
        {
            services.AddMediatRTimerManagerBehavior();
            services.AddScoped(typeof(IPipelineBehavior<CreateTimerCommand, TimerRequest>), typeof(TimerTransactionBehavior<CreateTimerCommand, TimerRequest>));
            services.AddScoped(typeof(IPipelineBehavior<CompleteTimerCommand, IOperationResult>), typeof(TimerTransactionBehavior<CompleteTimerCommand, IOperationResult>));
            return services;
        }

    }
}
