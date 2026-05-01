using Juice.AspNetCore.Mvc.Filters;
using Juice.MultiTenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
#if NET6_0
using Microsoft.OpenApi.Models;
#else
using Microsoft.OpenApi;
#endif

namespace Juice.Extensions.Swagger
{
    /// <summary>
    /// Hide docs for tenant or non-tenant api
    /// </summary>
    public class TenantDocsFilter : IDocumentFilter
    {
        private IHttpContextAccessor _httpContextAccessor;
        public TenantDocsFilter(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var tenant = _httpContextAccessor.HttpContext?.RequestServices.GetService<ITenantAccessor>()?.Tenant;
            foreach (var apiDescription in context.ApiDescriptions)
            {
                if (apiDescription.ActionDescriptor.FilterDescriptors
                    .Any(f => f.Filter is RequireTenantAttribute))
                {
                    if (tenant == null)
                    {
                        swaggerDoc.Paths.Remove("/" + apiDescription.RelativePath);
                    }
                }
                else if (apiDescription.ActionDescriptor.FilterDescriptors
                    .Any(f => f.Filter is RootTenantAttribute))
                {
                    if (tenant != null)
                    {
                        swaggerDoc.Paths.Remove("/" + apiDescription.RelativePath);
                    }
                }
            }
        }
    }
}
