using Juice.BgService.Management.File;
using Juice.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Juice.BgService.Api
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


        public static void SeparateStoreFile(this WebApplicationBuilder builder, string name)
        {
            builder.Services.UseOptionsMutableFileStore<FileStoreOptions>($"appsettings.{name}.{builder.Environment.EnvironmentName}.json");

            builder.Configuration.AddJsonFile($"appsettings.{name}.{builder.Environment.EnvironmentName}.json");

        }
    }
}
