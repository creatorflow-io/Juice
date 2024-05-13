using Juice.Domain;
namespace Juice.EF
{
    public interface IAuditableDbContext
    {
        /// <summary>
        /// Type of the event to be published by its name, leave null if you don't want to publish event. The name will be one of the following:
        /// <para><see cref="DataEvents.Inserted"/></para>
        /// <para><see cref="DataEvents.Modified"/></para>
        /// <para><see cref="DataEvents.Deleted"/></para>
        /// </summary>
        Type? EventType(string name);

        List<AuditEntry>? PendingAuditEntries { get; }

        string? User { get; }
    }
}
