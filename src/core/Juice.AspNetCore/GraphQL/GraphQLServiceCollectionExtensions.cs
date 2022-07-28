using GraphQL.Server;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class GraphQLServiceCollectionExtensions
    {
        public static IServiceCollection AddGraphQLWithDefaultConfigs(this IServiceCollection services)
        {
            services.AddGraphQL((options, provider) =>
            {
                options.EnableMetrics = true;
                var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("GraphQL");
                options.UnhandledExceptionDelegate = ctx => logger.LogError("{Error} occurred", ctx.OriginalException.Message);
            })
            .AddSystemTextJson()
            .AddGraphTypes(ServiceLifetime.Scoped)
            ;

            return services;
        }
    }
}
