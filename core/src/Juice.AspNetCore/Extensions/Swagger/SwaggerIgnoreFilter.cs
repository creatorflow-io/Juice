using Juice.CompnentModel;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Juice.Extensions.Swagger
{
    /// <summary>
    /// If you wan to ignore property in swagger doc but need property for JSON serialize/deserialize
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
                .Contains(k, new PropertyNameComparer()));

            foreach (var key in keys)
            {
                schema.Properties.Remove(key);
            }

        }

        #endregion
    }

    internal class PropertyNameComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            return x.Equals(y, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }
    }
}
