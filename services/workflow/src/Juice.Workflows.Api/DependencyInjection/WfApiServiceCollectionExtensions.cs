using Juice.Workflows.Api.IntegrationEvents.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Api.DependencyInjection
{
    public static class WfApiServiceCollectionExtensions
    {
        public static IServiceCollection AddWorkflowIntegrationEventHandlers(this IServiceCollection services)
        {
            services.AddTransient<TimerExpiredIntegrationEventHandler>();
            services.AddTransient<MessageCatchIntegrationEventHandler>();
            return services;
        }


    }
}
