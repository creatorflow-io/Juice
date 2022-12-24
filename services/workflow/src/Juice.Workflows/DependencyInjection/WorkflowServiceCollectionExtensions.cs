using Juice.Workflows.Builder;
using Juice.Workflows.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Workflows.DependencyInjection
{
    public static class WorkflowServiceCollectionExtensions
    {
        public static IServiceCollection AddWorkflowServices(this IServiceCollection services)
        {
            services.AddScoped<IWorkflow, Workflow>();

            services.AddScoped<WorkflowExecutor>();

            services.AddSingleton<NodeLibrary>();
            services.AddSingleton<INodeLibrary>(sp => sp.GetRequiredService<NodeLibrary>());

            services.RegisterNodes(
                typeof(StartEvent).Assembly.GetTypes()
                    .Where(t => !t.IsAbstract && t.IsAssignableTo(typeof(INode)))
                    .ToArray()
                );

            services.AddTransient<WorkflowContextBuilder>();
            services.AddSingleton<IncodeWorkflowContextBuilder>();

            services.AddScoped<IWorkflowContextBuilder>(sp =>
            {
                return sp.GetRequiredService<IncodeWorkflowContextBuilder>();
            });

            return services;
        }

        public static IServiceCollection RegisterWorkflow(this IServiceCollection services,
           string workflowId, Action<WorkflowContextBuilder> contextBuilder)
        {
            var tmpServiceProvider = services.BuildServiceProvider();

            var builder = tmpServiceProvider.GetRequiredService<WorkflowContextBuilder>();
            contextBuilder(builder);
            var manager = tmpServiceProvider.GetRequiredService<IncodeWorkflowContextBuilder>();
            manager.Register(workflowId, builder);
            return services;
        }


        public static IServiceCollection AddInMemoryReposistories(this IServiceCollection services)
        {
            services.TryAddSingleton<IWorkflowStateReposistory, InMemoryStateReposistory>();
            services.TryAddSingleton<IWorkflowRepository, InMemoryWorkflowReposistory>();

            return services;
        }


        public static IServiceCollection RegisterNodes(this IServiceCollection services,
            params Type[] types)
        {
            var library = services.BuildServiceProvider().GetRequiredService<INodeLibrary>();
            foreach (var type in types)
            {
                library.TryRegister(type);
                services.AddTransient(type);// node may appear multiple time in workflow process
            }
            return services;
        }
    }
}
