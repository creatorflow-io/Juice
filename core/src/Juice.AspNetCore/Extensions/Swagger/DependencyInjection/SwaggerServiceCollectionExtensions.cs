using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Swagger
{
    public static class SwaggerServiceCollectionExtensions
    {
        public static IServiceCollection AddSwaggerWithDefaultConfigs(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

                c.IgnoreObsoleteActions();

                c.IgnoreObsoleteProperties();

                c.SchemaFilter<SwaggerIgnoreFilter>();

                c.UseInlineDefinitionsForEnums();
            });

            services.AddSwaggerGenNewtonsoftSupport();

            return services;
        }
    }
}
