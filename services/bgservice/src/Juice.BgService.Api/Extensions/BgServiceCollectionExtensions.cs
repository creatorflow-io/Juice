using Juice.Extensions.Swagger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Juice.BgService.Api.Extensions
{
    public static class BgServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureBgServiceSwaggerGen(this IServiceCollection services)
        {
            services.ConfigureSwaggerGen(c =>
            {
                c.SwaggerDoc("bgservice-v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Background Service API V1",
                    Description = "Provide background service control API"
                });

                c.IncludeReferencedXmlComments();
            });
            return services;
        }

    }
}
