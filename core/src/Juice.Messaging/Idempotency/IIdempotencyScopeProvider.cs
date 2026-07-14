namespace Juice.Messaging.Idempotency
{
    /// <summary>
    /// Composes the idempotency <c>scope</c> from the server-resolved tenant (FR-011) and a base scope
    /// (endpoint id for HTTP, command type name for MediatR). Shared by both entry points so keys are
    /// partitioned identically regardless of where the request originates.
    /// </summary>
    public interface IIdempotencyScopeProvider
    {
        /// <summary>
        /// Resolve the tenant-partitioned scope for <paramref name="baseScope"/>. Result shape:
        /// <c>"{tenant}:{baseScope}"</c>, where a null tenant maps to a fixed unscoped sentinel.
        /// </summary>
        string Resolve(string baseScope);
    }

    /// <summary>
    /// Default <see cref="IIdempotencyScopeProvider"/>: prefixes the base scope with the server-resolved
    /// tenant from <see cref="IIdempotencyTenantProvider"/>. A null/blank tenant maps to
    /// <see cref="NoTenant"/>, a well-known partition that never collides with a real tenant.
    /// </summary>
    public sealed class DefaultIdempotencyScopeProvider : IIdempotencyScopeProvider
    {
        /// <summary>Partition used when no tenant is resolved.</summary>
        public const string NoTenant = "__notenant__";

        private readonly IIdempotencyTenantProvider _tenantProvider;

        public DefaultIdempotencyScopeProvider(IIdempotencyTenantProvider tenantProvider)
        {
            _tenantProvider = tenantProvider;
        }

        public string Resolve(string baseScope)
        {
            // Never trust a client-supplied tenant value: the id comes only from the server-side provider.
            var tenant = _tenantProvider.GetTenantId();
            if (string.IsNullOrWhiteSpace(tenant))
            {
                tenant = NoTenant;
            }
            return $"{tenant}:{baseScope}";
        }
    }
}
