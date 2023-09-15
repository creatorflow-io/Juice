using Juice.Audit.Domain.DataAuditAggregate;
using Juice.EF;

namespace Juice.Audit.EF.Repositories
{
    internal class DataAuditRepository : RepositoryBase<DataAudit, AuditDbContext>,
        IDataAuditRepository
    {
        public DataAuditRepository(AuditDbContext context) : base(context)
        {
        }

        public Task AddRangeAsync(IEnumerable<DataAudit> auditEntries, CancellationToken token)
        {
            Context.AuditEntries.AddRange(auditEntries);
            return Context.SaveChangesAsync(token);
        }
    }
}
