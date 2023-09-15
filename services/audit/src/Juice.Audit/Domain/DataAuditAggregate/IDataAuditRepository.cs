using Juice.Domain;

namespace Juice.Audit.Domain.DataAuditAggregate
{
    public interface IDataAuditRepository : IRepository<DataAudit>
    {
        Task AddRangeAsync(IEnumerable<DataAudit> auditEntries, CancellationToken token);
    }
}
