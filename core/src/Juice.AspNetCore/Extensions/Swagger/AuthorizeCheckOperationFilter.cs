using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.SwaggerGen;

#if !NET10_0
using Microsoft.OpenApi.Models;
#else
using Microsoft.OpenApi;
#endif

namespace Juice.Extensions.Swagger
{
    /// <summary>
    /// Add 401 and 403 response to swagger doc if api has authorize attribute
    /// </summary>
    public class AuthorizeCheckOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var hasAuthorize =
              (context.MethodInfo.DeclaringType?.GetCustomAttributes(true)?.OfType<AuthorizeAttribute>()?.Any() ?? false)
              || context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

            if (hasAuthorize)
            {
                operation.Responses?.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
                operation.Responses?.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
                var scopes = Array.Empty<string>();

                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
#if !NET10_0
                        [new OpenApiSecurityScheme {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                        }] = scopes.ToList()
#else
                        [new OpenApiSecuritySchemeReference("oauth2")] = scopes.ToList()
#endif
                    }
                };
            }
        }
    }

}
