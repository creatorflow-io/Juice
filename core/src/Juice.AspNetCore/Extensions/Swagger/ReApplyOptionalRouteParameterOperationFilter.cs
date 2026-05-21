using Swashbuckle.AspNetCore.SwaggerGen;
#if !NET10_0
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
#else
using Microsoft.OpenApi;
using System.Text.Json.Nodes;
#endif

namespace Juice.Extensions.Swagger
{
    /// <summary>
    /// Allow user to send empty value for optional route parameter by checking "Send empty value" in Swagger UI
    /// </summary>
    public class ReApplyOptionalRouteParameterOperationFilter : IOperationFilter
    {
        private const string _captureName = "routeParameter";

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {

            var httpMethodAttributes = context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>();

            var httpMethodWithOptional = httpMethodAttributes?.FirstOrDefault(m => m.Template?.Contains("?") ?? false);
            if (string.IsNullOrEmpty(httpMethodWithOptional?.Template))
                return;

            if(operation.Parameters == null || operation.Parameters.Count == 0)
                return;

            string regex = $"{{(?<{_captureName}>\\w+)\\?}}";

            var matches = System.Text.RegularExpressions.Regex.Matches(httpMethodWithOptional!.Template, regex);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var name = match.Groups[_captureName].Value;

#if !NET10_0
                var parameter = operation.Parameters.FirstOrDefault(p => p.In == ParameterLocation.Path && p.Name == name);
                if (parameter != null)
                {
                    parameter.AllowEmptyValue = true;
                    parameter.Description = $"Must check \"Send empty value\" or Swagger leaves '{{{name}}}' for empty values otherwise";
                    parameter.Required = false;
                    parameter.Schema.Default = new OpenApiString(string.Empty);
                    parameter.Schema.Nullable = true;
                }
#else
                var parameter = operation.Parameters.FirstOrDefault(p => p.In == ParameterLocation.Path && p.Name == name) as OpenApiParameter;
                if (parameter != null)
                {
                    parameter.AllowEmptyValue = true;
                    parameter.Description = $"Must check \"Send empty value\" or Swagger leaves '{{{name}}}' for empty values otherwise";
                    parameter.Required = false;
                    var schema = parameter.Schema as OpenApiSchema;
                    if (schema != null)
                    {
                        schema.Default = JsonValue.Create(string.Empty);
                        schema.Type = schema.Type.HasValue
                            ? schema.Type | JsonSchemaType.Null
                            : JsonSchemaType.String | JsonSchemaType.Null;
                    }
                }
#endif
            }
        }
    }
}
