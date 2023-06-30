using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Juice.Extensions.Swagger
{
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

            string regex = $"{{(?<{_captureName}>\\w+)\\?}}";

            var matches = System.Text.RegularExpressions.Regex.Matches(httpMethodWithOptional!.Template, regex);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var name = match.Groups[_captureName].Value;

                var parameter = operation.Parameters.FirstOrDefault(p => p.In == ParameterLocation.Path && p.Name == name);
                if (parameter != null)
                {
                    parameter.AllowEmptyValue = true;
                    parameter.Description = $"Must check \"Send empty value\" or Swagger leaves '{{{name}}}' for empty values otherwise";
                    parameter.Required = false;
                    parameter.Schema.Default = new OpenApiString(string.Empty);
                    parameter.Schema.Nullable = true;
                }
            }
        }
    }
}
