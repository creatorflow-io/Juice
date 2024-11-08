using Newtonsoft.Json;
using Juice.CompnentModel;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Juice.Extensions.Swagger
{
    /// <summary>
    /// If you want to ignore property in swagger doc but need property for JSON serialize/deserialize
    /// <para>so you could not use <see cref="JsonIgnoreAttribute" /> but use <see cref="ApiIgnoreAttribute"/> instead.</para>
    /// <para><see cref="SwaggerIgnoreFilter"/> will exclude all properties that have <see cref="ApiIgnoreAttribute"/> </para>
    /// </summary>
    public class SwaggerIgnoreFilter : ISchemaFilter
    {
        #region ISchemaFilter Members

        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            var excludedProperties = context.Type.GetProperties()
                                         .Where(t =>
                                                t.HasAttribute<ApiIgnoreAttribute>())
                                         .ToArray();

            var keys = schema.Properties.Keys.Where(k => excludedProperties.Select(p => p.Name)
                .Contains(k, StringComparer.OrdinalIgnoreCase));

            foreach (var key in keys)
            {
                schema.Properties.Remove(key);
            }

        }

        #endregion
    }
}
