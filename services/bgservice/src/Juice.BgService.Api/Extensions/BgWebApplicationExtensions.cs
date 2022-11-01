using Microsoft.AspNetCore.Builder;

namespace Juice.BgService.Api.Extensions
{
    public static class BgWebApplicationExtensions
    {
        public static void UseBgServiceSwaggerUI(this WebApplication app)
        {
            app.UseSwagger(options => options.RouteTemplate = "bgservice/swagger/{documentName}/swagger.json");
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("bgservice-v1/swagger.json", "Background Service API V1");
                c.RoutePrefix = "bgservice/swagger";
            });
        }
    }
}
