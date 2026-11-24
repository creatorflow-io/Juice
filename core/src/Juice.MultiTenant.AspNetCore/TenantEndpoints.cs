using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.MultiTenant.AspNetCore
{
    /// <summary>
    /// Writes a refused tenant. The default is a problem document (<c>application/problem+json</c>) with the code as <c>code</c>; a service
    /// with its own error shape registers another.
    /// </summary>
    public interface ITenantRejectionWriter
    {
        Task WriteAsync(HttpContext http, TenantRejection rejection);
    }

    internal sealed class ProblemTenantRejectionWriter : ITenantRejectionWriter
    {
        public Task WriteAsync(HttpContext http, TenantRejection rejection)
            => Results.Problem(statusCode: rejection.StatusCode, title: rejection.Title,
                extensions: new Dictionary<string, object?> { ["code"] = rejection.Code }).ExecuteAsync(http);
    }

    /// <summary>What an endpoint needs of the request's tenant.</summary>
    public enum TenantRequirement
    {
        /// <summary>
        /// A tenant the caller's token allows (checked by the middleware); who may act in it is the endpoint's business. A caller whose token
        /// names no tenant (a service) passes, so check such callers yourself, or use <see cref="Member"/> or <see cref="Admin"/>.
        /// </summary>
        Tenant,

        /// <summary>
        /// The caller belongs to it (<see cref="ITenantAccess.IsMemberAsync"/>): their own data in it. A platform admin isn't a member of a
        /// tenant; give admin endpoints <see cref="Admin"/>.
        /// </summary>
        Member,

        /// <summary>The caller administers it (<see cref="ITenantAccess.CanAdministerAsync"/>); a platform admin administers every tenant.</summary>
        Admin,
    }

    /// <summary>The authorization policies of the tenant pattern, for gRPC services and controllers (and anything else policies reach).</summary>
    public static class TenancyPolicies
    {
        public const string Tenant = "juice-tenancy:tenant";
        public const string Member = "juice-tenancy:member";
        public const string Admin = "juice-tenancy:admin";

        internal static string For(TenantRequirement requirement) => requirement switch
        {
            TenantRequirement.Member => Member,
            TenantRequirement.Admin => Admin,
            _ => Tenant,
        };
    }

    internal static class TenantRequirements
    {
        /// <summary>Null when the request meets it, else why not. An unknown tenant and another tenant get the same 403.</summary>
        public static async ValueTask<TenantRejection?> CheckAsync(HttpContext http, TenantRequirement requirement)
        {
            var tenant = http.RequestServices.GetRequiredService<ITenantContext>().Tenant;
            if (tenant is null)
            {
                return TenantRejection.Required();
            }
            // Again here: the caller may have been signed in by a scheme that ran after the middleware.
            var options = http.RequestServices.GetRequiredService<IOptions<TenancyOptions>>().Value;
            if (TenantBinding.Check(http.User, options, tenant.Id) is { } refused)
            {
                return refused;
            }
            var access = http.RequestServices.GetRequiredService<ITenantAccess>();
            var allowed = requirement switch
            {
                TenantRequirement.Member => await access.IsMemberAsync(http.User, tenant.Id, http.RequestAborted),
                TenantRequirement.Admin => await access.CanAdministerAsync(http.User, tenant.Id, http.RequestAborted),
                _ => true,
            };
            return allowed ? null : TenantRejection.Forbidden(tenant.Id);
        }

        public static Task WriteAsync(HttpContext http, TenantRejection rejection)
            => http.RequestServices.GetRequiredService<ITenantRejectionWriter>().WriteAsync(http, rejection);
    }

    /// <summary>Minimal-API endpoints that need the request's tenant (endpoint filters: for gRPC services, use <see cref="TenancyPolicies"/>).</summary>
    public static class TenantEndpointConventionExtensions
    {
        /// <summary>The endpoints need a tenant (400 <c>tenant_required</c> without one).</summary>
        public static TBuilder RequireTenant<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
            => builder.Require(TenantRequirement.Tenant);

        /// <summary>The endpoints are for the tenant's members (403 <c>tenant_forbidden</c> otherwise).</summary>
        public static TBuilder RequireTenantMember<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
            => builder.Require(TenantRequirement.Member);

        /// <summary>The endpoints are for the tenant's admins (403 <c>tenant_forbidden</c> otherwise).</summary>
        public static TBuilder RequireTenantAdmin<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
            => builder.Require(TenantRequirement.Admin);

        private static TBuilder Require<TBuilder>(this TBuilder builder, TenantRequirement requirement) where TBuilder : IEndpointConventionBuilder
            => builder.AddEndpointFilter(async (context, next) =>
            {
                var rejection = await TenantRequirements.CheckAsync(context.HttpContext, requirement);
                if (rejection is null)
                {
                    return await next(context);
                }
                return new TenantRejectionResult(rejection);
            });
    }

    /// <summary>A refused tenant as a result (minimal APIs and MVC), written by the <see cref="ITenantRejectionWriter"/>.</summary>
    public sealed class TenantRejectionResult(TenantRejection rejection) : IResult, IActionResult
    {
        public TenantRejection Rejection { get; } = rejection;

        public Task ExecuteAsync(HttpContext httpContext) => TenantRequirements.WriteAsync(httpContext, Rejection);

        public Task ExecuteResultAsync(ActionContext context) => ExecuteAsync(context.HttpContext);
    }

    /// <summary>MVC controllers or actions that need the request's tenant.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireTenantAttribute(TenantRequirement requirement = TenantRequirement.Tenant) : Attribute, IAsyncActionFilter
    {
        public TenantRequirement Requirement { get; } = requirement;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var rejection = await TenantRequirements.CheckAsync(context.HttpContext, Requirement);
            if (rejection is null)
            {
                await next();
                return;
            }
            context.Result = new TenantRejectionResult(rejection);
        }
    }

    /// <summary>The requirement behind <see cref="TenancyPolicies"/>.</summary>
    public sealed class TenantAuthorizationRequirement(TenantRequirement requirement) : IAuthorizationRequirement
    {
        public TenantRequirement Requirement { get; } = requirement;
    }

    internal sealed class TenantAuthorizationHandler(IHttpContextAccessor accessor) : AuthorizationHandler<TenantAuthorizationRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantAuthorizationRequirement requirement)
        {
            var http = context.Resource as HttpContext ?? accessor.HttpContext;
            if (http is null)
            {
                return;
            }
            if (await TenantRequirements.CheckAsync(http, requirement.Requirement) is { } rejection)
            {
                // A failed policy is a plain 403 (gRPC: PermissionDenied); the reason carries the code for a result handler that maps it.
                context.Fail(new AuthorizationFailureReason(this, rejection.Code));
                return;
            }
            context.Succeed(requirement);
        }
    }
}
