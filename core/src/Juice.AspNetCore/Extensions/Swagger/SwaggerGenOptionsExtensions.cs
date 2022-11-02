using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Juice.Extensions.Swagger
{
    public static class SwaggerGenOptionsExtensions
    {
        public static void IncludeReferencedXmlComments(this SwaggerGenOptions c)
        {
            var currentAssembly = Assembly.GetCallingAssembly();
            var xmlDocs = currentAssembly.GetReferencedAssemblies()
            .Union(new AssemblyName[] { currentAssembly.GetName() })
            .Select(a => Path.Combine(AppContext.BaseDirectory, $"{a.Name}.xml"))
            .Where(f => File.Exists(f)).ToArray();

            foreach (var xmlPath in xmlDocs)
            {
                c.IncludeXmlComments(xmlPath);
            }
        }
    }
}
