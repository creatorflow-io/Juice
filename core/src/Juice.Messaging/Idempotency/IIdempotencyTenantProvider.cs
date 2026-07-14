namespace Juice.Messaging.Idempotency
{
    /// <summary>
    /// Supplies the <b>server-resolved</b> tenant identifier used to partition idempotency keys (FR-011),
    /// shared by every idempotency entry point (the ASP.NET Core HTTP filters and the MediatR behavior).
    /// <para></para>
    /// This is an abstraction so the idempotency layers stay decoupled from any concrete multi-tenancy
    /// library (Constitution II). Hosts using Finbuckle register a provider that reads the resolved tenant
    /// context; the default returns <c>null</c> (non-multi-tenant hosts). A client-supplied tenant value
    /// MUST NOT be used here — only the server-authenticated tenant.
    /// </summary>
    public interface IIdempotencyTenantProvider
    {
        /// <summary>The current tenant id, or <c>null</c> when no tenant is resolved.</summary>
        string? GetTenantId();
    }

    /// <summary>
    /// Default provider for non-multi-tenant hosts: always returns <c>null</c>, which the scope provider
    /// maps to a single well-known unscoped partition.
    /// </summary>
    public sealed class NullIdempotencyTenantProvider : IIdempotencyTenantProvider
    {
        public string? GetTenantId() => null;
    }
}
