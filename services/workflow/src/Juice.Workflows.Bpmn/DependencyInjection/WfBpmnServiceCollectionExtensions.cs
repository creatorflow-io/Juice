using Juice.Workflows.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Bpmn
{
    public static class WfBpmnServiceCollectionExtensions
    {
        public static IServiceCollection RegisterBpmnWorkflows(this IServiceCollection services, string? directory = "workflows")
        {
            services.AddScoped<Builder.WorkflowContextBuilder>();
            services.AddScoped<Builder.BpmnFileWorkflowContextBuilder>();
            services.AddScoped<IWorkflowContextBuilder>(sp =>
            {
                var builder = sp.GetRequiredService<Builder.BpmnFileWorkflowContextBuilder>();
                builder.SetWorkflowsDirectory(directory);
                return builder;
            });

            return services;
        }
    }
}
