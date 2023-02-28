using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Api.DependencyInjection
{
    public static class WfApiServiceCollectionExtensions
    {
        public static IServiceCollection AddWorkflowCommandHandlers(this IServiceCollection services)
        {
            return services;
        }
    }
}
