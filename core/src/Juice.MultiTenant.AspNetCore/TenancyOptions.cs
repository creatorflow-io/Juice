namespace Juice.MultiTenant.AspNetCore
{
    /// <summary>
    /// The names of the tenant pattern (the tenant pattern decision record), each overridable. Bind it from the <c>Tenancy</c> section, or
    /// set it in <c>AddJuiceTenancy</c>.
    /// </summary>
    public class TenancyOptions
    {
        /// <summary>The claim holding the caller's tenant Id.</summary>
        public string TenantClaim { get; set; } = "platform_tenant";

        /// <summary>The header naming the tenant on a call from a platform admin or a service (REST or gRPC).</summary>
        public string Header { get; set; } = "X-Tenant-Id";

        /// <summary>
        /// The header older Juice gRPC clients send (<c>__tenant__</c>). Read only when its value is a tenant Id; an Identifier there is
        /// ignored. Null turns it off. It goes away in the next major version.
        /// </summary>
        public string? LegacyHeader { get; set; } = Constants.TenantToken;

        /// <summary>The route parameter naming the tenant (<c>/tenants/{tenantId}</c>).</summary>
        public string RouteParameter { get; set; } = "tenantId";

        /// <summary>The role that may act in any tenant, naming it explicitly.</summary>
        public string PlatformAdminRole { get; set; } = "platform-admin";

        /// <summary>The service's tenant-admin role: its holder administers the tenant its claim names. Null when the service has none.</summary>
        public string? TenantAdminRole { get; set; }

        /// <summary>
        /// Other roles that act across tenants, naming the tenant explicitly (service accounts). A caller with one isn't bound to its claim,
        /// and sees an unknown tenant as 404 like a platform admin.
        /// </summary>
        public IList<string> CrossTenantRoles { get; set; } = [];

        /// <summary>
        /// A single-tenant deployment's tenant (Juice.Observability's <c>JUICE_PLATFORM_TENANT_ID</c>): every request is in it, a token for
        /// another tenant is refused, and a route or header naming another tenant is too.
        /// </summary>
        public Guid? FixedTenantId { get; set; }

        /// <summary>
        /// Refuse a signed-in caller whose token names no tenant (and has no cross-tenant role), with 403 <c>tenant_forbidden</c>. For services
        /// whose every client carries the claim (Juice.Observability). Otherwise such a caller (a service's own token) is left to the
        /// service's authorization.
        /// </summary>
        public bool RequireClaim { get; set; }

        /// <summary>
        /// Let an anonymous caller name a tenant in the route or header (a public endpoint per tenant). Off by default. When on, whatever is
        /// wrong with the tenant (unknown, inactive, starting) is the same 403 <c>tenant_forbidden</c>, so nothing about it is revealed.
        /// </summary>
        public bool AnonymousMayNameTenant { get; set; }

        /// <summary>How long a tenant found in the directory is remembered. Lifecycle events evict it sooner.</summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>How long a tenant the directory doesn't know is remembered as unknown.</summary>
        public TimeSpan UnknownCacheDuration { get; set; } = TimeSpan.FromSeconds(15);
    }
}
