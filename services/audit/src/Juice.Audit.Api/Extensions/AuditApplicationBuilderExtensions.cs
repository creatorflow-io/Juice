using Juice.Audit.Api.Grpc.Services;
using Juice.Audit.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Juice.Audit.AspNetCore.Extensions
{
    public static class AuditApplicationBuilderExtensions
    {
        /// <summary>
        /// You can enable audit for specific paths by passing the prefixes or methods options
        /// <para>We should put <c>UseAudit()</c> after <c>UseStaticFiles()</c> to avoid static file requests</para>
        /// </summary>
        /// <param name="app"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseAudit(this IApplicationBuilder app,
            string appName, Action<AuditFilterOptions>? configure = default)
        {
            var options = new AuditFilterOptions();
            configure?.Invoke(options);

            if (options.Filters.Length == 0)
            {
                app.UseMiddleware<AuditMiddleware>();
            }
            else
            {
                app.UseWhen(context =>
                        options.IsMatch(
                            context.Request.Path, context.Request.Method
                            ),
                            appBuilder => appBuilder.UseMiddleware<AuditMiddleware>(appName)
                );
            }
            return app;
        }

        /// <summary>
        /// You can enable audit for specific paths by passing the prefixes or methods options.
        /// <para>We should put <c>UseAudit()</c> after <c>UseStaticFiles()</c> to avoid static file requests</para>
        /// </summary>
        /// <param name="app"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static WebApplication UseAudit(this WebApplication app,
            string appName, Action<AuditFilterOptions>? configure = default)
        {
            var options = new AuditFilterOptions();
            configure?.Invoke(options);

            if (options.Filters.Length == 0)
            {
                app.UseMiddleware<AuditMiddleware>(appName, options);
            }
            else
            {
                app.UseWhen(context =>
                        options.IsMatch(
                            context.Request.Path, context.Request.Method
                            ),
                            appBuilder => appBuilder.UseMiddleware<AuditMiddleware>(appName, options)
                );
            }
            return app;
        }

        /// <summary>
        /// Map the gRPC Audit server to handle audit messages from clients
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IEndpointRouteBuilder MapAuditGrpcServer(this IEndpointRouteBuilder app)
        {
            app.MapGrpcService<AuditGrpcServer>();
            return app;
        }

    }
}
