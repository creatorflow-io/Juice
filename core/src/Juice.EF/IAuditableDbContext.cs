namespace Juice.EF
{
    public interface IAuditableDbContext
    {
        List<AuditEntry>? PendingAuditEntries { get; }

        string? User { get; }
    }
}
