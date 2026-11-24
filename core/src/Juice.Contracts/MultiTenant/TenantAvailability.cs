namespace Juice.MultiTenant
{
    /// <summary>Whether a tenant may be served now (the tenant pattern decision record, section 3).</summary>
    public enum TenantAvailability
    {
        /// <summary>Serve it.</summary>
        Active,

        /// <summary>Still being set up: answer 503 <c>tenant_starting</c>, and hold background work.</summary>
        Starting,

        /// <summary>Inactive, suspended, abandoned or never approved: answer 403 <c>tenant_inactive</c>, and hold background work.</summary>
        Inactive,
    }

    /// <summary>
    /// A tenant whose store knows its status, by juice-tenants' status names (<c>Active</c>, <c>Initializing</c>, <c>Suspended</c>…). A tenant
    /// without it (a configuration or in-memory store) is <see cref="TenantAvailability.Active"/>.
    /// </summary>
    public interface ITenantStatus
    {
        string? Status { get; }
    }

    public static class TenantStatuses
    {
        /// <summary>
        /// The availability of a juice-tenants status: <c>Active</c>, or none, is active; <c>Initializing</c>, <c>Initialized</c> and
        /// <c>PendingToActive</c> are starting; anything else is inactive.
        /// </summary>
        public static TenantAvailability Availability(string? status)
        {
            if (string.IsNullOrWhiteSpace(status) || status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                return TenantAvailability.Active;
            }
            return status.Trim().ToLowerInvariant() switch
            {
                "initializing" or "initialized" or "pendingtoactive" => TenantAvailability.Starting,
                _ => TenantAvailability.Inactive,
            };
        }

        /// <summary>The availability of a tenant: by its <see cref="ITenantStatus.Status"/> when its store knows one, else active.</summary>
        public static TenantAvailability Availability(object? tenant)
            => tenant is ITenantStatus withStatus ? Availability(withStatus.Status) : TenantAvailability.Active;
    }
}
