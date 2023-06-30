using Juice.MultiTenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// Return notfound if tenant is mismatched
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireTenantAttribute : Attribute, IAsyncResourceFilter
    {
        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var tenant = context.HttpContext.RequestServices.GetService<ITenant?>();
            if (tenant == null)
            {
                context.Result = new NotFoundResult();
                return;
            }
            await next();
        }
    }
}
