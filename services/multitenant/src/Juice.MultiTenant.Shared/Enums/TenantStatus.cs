namespace Juice.MultiTenant.Shared.Enums
{
    public enum TenantStatus
    {
        New,
        PendingApproval,
        Approved,
        Rejected,
        Initializing,
        Initialized,
        PendingToActive,
        Inactive,
        Active,
        Suspended,
        Abandoned
    }
}
