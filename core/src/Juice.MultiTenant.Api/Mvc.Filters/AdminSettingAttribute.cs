using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Api.Mvc.Filters
{
    /// <summary>
    /// Find tenant from route and set to ITenantSettingsRepository and TenantSettingsDbContext
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AdminSettingAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var tenantDbContext =
               context.HttpContext.RequestServices.GetRequiredService<TenantStoreDbContext>();
            var repository =
                context.HttpContext.RequestServices.GetRequiredService<ITenantSettingsRepository>();

            var id = context.RouteData.Values["id"]?.ToString();
            if (!string.IsNullOrEmpty(id))
            {
                var tenant = await tenantDbContext.TenantInfo.FindAsync(id, context.HttpContext.RequestAborted);
                if (tenant == null)
                {
                    context.Result = new NotFoundResult();
                    return;
                }
                repository.EnforceTenant(tenant);
            }

            await next();
        }
    }
}
