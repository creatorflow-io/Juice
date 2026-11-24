using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.MultiTenant.AspNetCore
{
    /// <summary>Where the request's tenant came from.</summary>
    public enum TenantSource
    {
        Route,
        Header,
        Claim,
        Fixed,
    }

    /// <summary>The request's tenant, once <see cref="TenantResolutionMiddleware"/> found it (and checked it). Scoped.</summary>
    public interface ITenantContext
    {
        TenantRecord? Tenant { get; }

        TenantSource? Source { get; }
    }

    internal sealed class TenantContext : ITenantContext
    {
        public TenantRecord? Tenant { get; set; }

        public TenantSource? Source { get; set; }
    }

    /// <summary>Why a request's tenant was refused; written by the <see cref="ITenantRejectionWriter"/>.</summary>
    public sealed record TenantRejection(int StatusCode, string Code, string Title, Guid? TenantId)
    {
        /// <summary>A route or header value that isn't a tenant Id.</summary>
        public static TenantRejection Invalid() => new(StatusCodes.Status400BadRequest, "tenant_invalid", "The tenant isn't a tenant Id.", null);

        /// <summary>The endpoint needs a tenant and none was named.</summary>
        public static TenantRejection Required() => new(StatusCodes.Status400BadRequest, "tenant_required", "Name the tenant.", null);

        /// <summary>Another tenant than the caller's, or one the caller may not see (unknown tenants look the same, so none is revealed).</summary>
        public static TenantRejection Forbidden(Guid? id) => new(StatusCodes.Status403Forbidden, "tenant_forbidden", "Not your tenant.", id);

        /// <summary>An unknown tenant, to a caller who may see every tenant.</summary>
        public static TenantRejection NotFound(Guid id) => new(StatusCodes.Status404NotFound, "tenant_not_found", "No such tenant.", id);

        public static TenantRejection Starting(Guid id)
            => new(StatusCodes.Status503ServiceUnavailable, "tenant_starting", "The tenant is still being set up; try again later.", id);

        public static TenantRejection Inactive(Guid id) => new(StatusCodes.Status403Forbidden, "tenant_inactive", "The tenant isn't active.", id);
    }

    /// <summary>
    /// Finds the request's tenant by the decision's rules, checks it, and sets <see cref="ITenantContext"/> (and Finbuckle's context when the
    /// directory is a Finbuckle store). Put it after routing and authentication, and before authorization:
    /// <code>app.UseRouting(); app.UseAuthentication(); app.UseJuiceTenancy(); app.UseAuthorization();</code>
    /// <list type="bullet">
    /// <item>A caller whose token names a tenant is in it; naming another is refused, and so is a claim that isn't a tenant Id.</item>
    /// <item>A platform admin or cross-tenant service names the tenant in the route or header; naming none, the request has no tenant.</item>
    /// <item>A signed-in caller whose token names no tenant (a service's own token) may name one; who may act in it is the endpoint's
    /// business. <see cref="TenancyOptions.RequireClaim"/> refuses such callers instead.</item>
    /// <item>An anonymous caller may not name a tenant, unless <see cref="TenancyOptions.AnonymousMayNameTenant"/>; then whatever is wrong
    /// with the tenant is the same 403, so nothing about it is revealed.</item>
    /// <item>A single-tenant deployment is always in its tenant; naming another tenant is refused.</item>
    /// <item>Then: unknown is 404 to whoever may see every tenant and 403 to the rest; starting is 503; otherwise not active is 403.</item>
    /// </list>
    /// A request naming no tenant passes through without one; endpoints that need one say so (<c>RequireTenant()</c>).
    /// </summary>
    public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext http, ITenantDirectory directory, IOptions<TenancyOptions> options,
            ITenantRejectionWriter writer)
        {
            var o = options.Value;
            if (!await AuthenticateForEndpointAsync(http))
            {
                // An endpoint that needs a signed-in caller and has none (no token, or an expired one): authorization answers 401, so the
                // client knows to sign in again rather than reading a 403 as the wrong tenant.
                await next(http);
                return;
            }
            var (outcome, tenantId, source) = Locate(http, o);
            if (outcome is not null)
            {
                await writer.WriteAsync(http, outcome);
                return;
            }
            if (tenantId is not { } id)
            {
                await next(http);
                return;
            }

            TenantRecord? tenant;
            Func<IDisposable?>? enter = null;
            if (directory is ITenantContextBridge bridge)
            {
                (tenant, enter) = await bridge.FindForRequestAsync(id);
            }
            else
            {
                tenant = await directory.FindAsync(id, http.RequestAborted);
            }
            var rejection = tenant switch
            {
                null => TenantCaller.ActsAcrossTenants(http.User, o) ? TenantRejection.NotFound(id) : TenantRejection.Forbidden(id),
                { Availability: TenantAvailability.Starting } => TenantRejection.Starting(id),
                { Availability: TenantAvailability.Inactive } => TenantRejection.Inactive(id),
                _ => null,
            };
            if (rejection is not null && http.User.Identity?.IsAuthenticated != true)
            {
                rejection = TenantRejection.Forbidden(id); // nothing about a tenant is revealed to an anonymous caller
            }
            if (rejection is not null)
            {
                logger.LogInformation("Tenant {TenantId} refused: {Code}", id, rejection.Code);
                await writer.WriteAsync(http, rejection);
                return;
            }

            var context = http.RequestServices.GetRequiredService<TenantContext>();
            context.Tenant = tenant;
            context.Source = source;
            // Finbuckle's context too, for code that reads the tenant through it (per-tenant options, Juice.EF.MultiTenant): the tenant the
            // directory just found, not a second lookup.
            using var finbuckle = enter?.Invoke();
            await next(http);
        }

        /// <summary>
        /// An endpoint that names its own authentication schemes (<c>[Authorize(AuthenticationSchemes = ...)]</c>, a gRPC service's JWT
        /// scheme) signs its caller in only when authorization runs, after this middleware. Sign the caller in with those schemes first, as
        /// the authorization middleware would, so the tenant is decided for the real caller.
        /// </summary>
        /// <returns>False when the endpoint needs a signed-in caller and there is none.</returns>
        private static async Task<bool> AuthenticateForEndpointAsync(HttpContext http)
        {
            var endpoint = http.GetEndpoint();
            if (endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                return true;
            }
            var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            var endpointPolicies = endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>();
            var provider = http.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();
            // An endpoint with no authorization of its own falls under the fallback policy, if the host sets one (as the authorization
            // middleware does).
            var policy = authorizeData.Count == 0 && endpointPolicies.Count == 0
                ? await provider.GetFallbackPolicyAsync()
                : await AuthorizationPolicy.CombineAsync(provider, authorizeData, endpointPolicies);
            if (policy is null)
            {
                return true;
            }
            if (policy is not null && policy.AuthenticationSchemes.Count > 0)
            {
                // Sets http.User to the schemes' principal (merged), as PolicyEvaluator does for the authorization middleware.
                await http.RequestServices.GetRequiredService<IPolicyEvaluator>().AuthenticateAsync(policy, http);
            }
            return http.User.Identity?.IsAuthenticated == true;
        }

        /// <summary>The tenant the request names, by the rules above, before it is looked up.</summary>
        internal static (TenantRejection? Rejection, Guid? TenantId, TenantSource? Source) Locate(HttpContext http, TenancyOptions o)
        {
            Guid? named = null;
            TenantSource? source = null;
            if (http.GetRouteValue(o.RouteParameter) is { } routeValue)
            {
                if (!TenantIds.TryParse(routeValue.ToString(), out var id))
                {
                    return (TenantRejection.Invalid(), null, null);
                }
                (named, source) = (id, TenantSource.Route);
            }
            else if (http.Request.Headers.TryGetValue(o.Header, out var header) && header.Count > 0)
            {
                if (!TenantIds.TryParse(header.ToString(), out var id))
                {
                    return (TenantRejection.Invalid(), null, null);
                }
                (named, source) = (id, TenantSource.Header);
            }
            else if (o.LegacyHeader is not null && http.Request.Headers.TryGetValue(o.LegacyHeader, out var legacy)
                && TenantIds.TryParse(legacy.ToString(), out var legacyId))
            {
                (named, source) = (legacyId, TenantSource.Header);
            }

            var user = http.User;
            var across = TenantCaller.ActsAcrossTenants(user, o);
            if (o.FixedTenantId is { } fixedId)
            {
                if (named is { } other && other != fixedId)
                {
                    return (across ? TenantRejection.NotFound(other) : TenantRejection.Forbidden(other), null, null);
                }
                (named, source) = (fixedId, named is null ? TenantSource.Fixed : source);
            }
            if (across)
            {
                return (null, named, source);
            }

            if (user.FindFirst(o.TenantClaim)?.Value is { } claim)
            {
                if (!TenantIds.TryParse(claim, out var mine) || (named is { } other && other != mine))
                {
                    return (TenantRejection.Forbidden(named), null, null);
                }
                return (null, mine, source ?? TenantSource.Claim);
            }
            if (user.Identity?.IsAuthenticated == true)
            {
                return o.RequireClaim ? (TenantRejection.Forbidden(named), null, null) : (null, named, source);
            }
            // Anonymous: a single-tenant deployment is still in its tenant; naming one is allowed only when the service says so.
            if (named is not null && source != TenantSource.Fixed && named != o.FixedTenantId && !o.AnonymousMayNameTenant)
            {
                return (TenantRejection.Forbidden(named), null, null);
            }
            return (null, named, source);
        }
    }

    internal static class TenantBinding
    {
        /// <summary>
        /// Null when the caller's token lets them be in the tenant, else why not. The endpoint checks repeat it, for a caller signed in by a
        /// scheme that ran after the middleware (a scheme an endpoint picks).
        /// </summary>
        public static TenantRejection? Check(ClaimsPrincipal user, TenancyOptions o, Guid tenant)
        {
            if (TenantCaller.ActsAcrossTenants(user, o))
            {
                return null;
            }
            if (user.FindFirst(o.TenantClaim)?.Value is { } claim)
            {
                return TenantIds.TryParse(claim, out var mine) && mine == tenant ? null : TenantRejection.Forbidden(tenant);
            }
            return user.Identity?.IsAuthenticated == true && o.RequireClaim ? TenantRejection.Forbidden(tenant) : null;
        }
    }
}
