using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace Juice.MultiTenant.AspNetCore
{
    /// <summary>
    /// Who may do what in a tenant. Every tenant-scoped endpoint asks this one question, so how it is answered can change without touching
    /// them: today from the token (<see cref="ClaimTenantAccess"/>); later from membership kept elsewhere, by registering another
    /// implementation.
    /// </summary>
    public interface ITenantAccess
    {
        /// <summary>Whether the caller belongs to the tenant (their own data in it).</summary>
        ValueTask<bool> IsMemberAsync(ClaimsPrincipal user, Guid tenantId, CancellationToken ct = default);

        /// <summary>Whether the caller administers the tenant.</summary>
        ValueTask<bool> CanAdministerAsync(ClaimsPrincipal user, Guid tenantId, CancellationToken ct = default);

        /// <summary>Which tenants the caller administers: every tenant, or the ones listed. It agrees with <see cref="CanAdministerAsync"/>.</summary>
        ValueTask<AdministeredTenants> AdministeredAsync(ClaimsPrincipal user, CancellationToken ct = default);
    }

    /// <summary>The tenants a caller administers: <see cref="Every"/> (a platform admin), or those in <see cref="Ids"/>.</summary>
    public sealed record AdministeredTenants(bool Every, IReadOnlyList<Guid> Ids)
    {
        public static AdministeredTenants None { get; } = new(false, []);

        public static AdministeredTenants All { get; } = new(true, []);

        public bool Includes(Guid tenantId) => Every || Ids.Contains(tenantId);
    }

    /// <summary>
    /// The token decides: a platform admin administers every tenant; the service's tenant-admin role administers the one tenant the claim
    /// names; a member is anyone whose claim names the tenant. One tenant per token.
    /// </summary>
    public sealed class ClaimTenantAccess(IOptions<TenancyOptions> options) : ITenantAccess
    {
        public ValueTask<bool> IsMemberAsync(ClaimsPrincipal user, Guid tenantId, CancellationToken ct = default)
            => ValueTask.FromResult(TenantCaller.ClaimedTenant(user, options.Value) == tenantId);

        public ValueTask<bool> CanAdministerAsync(ClaimsPrincipal user, Guid tenantId, CancellationToken ct = default)
            => ValueTask.FromResult(Administered(user).Includes(tenantId));

        public ValueTask<AdministeredTenants> AdministeredAsync(ClaimsPrincipal user, CancellationToken ct = default)
            => ValueTask.FromResult(Administered(user));

        /// <summary>The one rule both answers come from, so they can't disagree.</summary>
        private AdministeredTenants Administered(ClaimsPrincipal user)
        {
            var o = options.Value;
            if (user.IsInRole(o.PlatformAdminRole))
            {
                return AdministeredTenants.All;
            }
            return o.TenantAdminRole is not null && user.IsInRole(o.TenantAdminRole) && TenantCaller.ClaimedTenant(user, o) is { } id
                ? new AdministeredTenants(false, [id])
                : AdministeredTenants.None;
        }
    }

    /// <summary>How a caller relates to tenants (section 3 of the decision).</summary>
    public static class TenantCaller
    {
        /// <summary>The tenant the caller's claim names, or null when it names none (or not an Id).</summary>
        public static Guid? ClaimedTenant(ClaimsPrincipal? user, TenancyOptions options)
            => TenantIds.TryParse(user?.FindFirst(options.TenantClaim)?.Value, out var id) ? id : null;

        /// <summary>A platform admin, or a cross-tenant service: it names the tenant explicitly and isn't bound to a claim.</summary>
        public static bool ActsAcrossTenants(ClaimsPrincipal? user, TenancyOptions options)
            => user is not null && (user.IsInRole(options.PlatformAdminRole) || options.CrossTenantRoles.Any(user.IsInRole)
                || (options.CrossTenantCaller?.Invoke(user) ?? false));
    }
}
