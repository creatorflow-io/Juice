using Juice.Workflows.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Yaml.DependencyInjection
{
    public static class WfYamlServiceCollectionExtensions
    {
        public static IServiceCollection RegisterYamlWorkflows(this IServiceCollection services, string? directory = "workflows")
        {
            services.AddScoped<Builder.YamlWorkflowContextBuilder>();
            services.AddScoped<IWorkflowContextBuilder>(sp =>
            {
                var builder = sp.GetRequiredService<Builder.YamlWorkflowContextBuilder>();
                builder.SetWorkflowsDirectory(directory);
                return builder;
            });

            return services;
        }
    }
}
