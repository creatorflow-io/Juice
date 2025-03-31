using Finbuckle.MultiTenant.AspNetCore.Internal;
using Juice.MultiTenant.TestHelper.Internal;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TenantTestServiceProviderExtensions
    {
        public static async Task TenantInvokeAsync(this IServiceProvider serviceProvider, RequestDelegate next)
        {
            using var scope = serviceProvider.CreateScope();
            HttpContext httpContext = new MyHttpContext(scope.ServiceProvider);
            await new MultiTenantMiddleware(next).Invoke(httpContext);
        }
    }
}
