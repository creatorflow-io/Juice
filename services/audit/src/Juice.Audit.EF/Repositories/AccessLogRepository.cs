using Juice.Audit.Domain.AccessLogAggregate;
using Juice.EF;

namespace Juice.Audit.EF.Repositories
{
    internal class AccessLogRepository : RepositoryBase<AccessLog, AuditDbContext>, IAccessLogRepository
    {
        public AccessLogRepository(AuditDbContext context) : base(context)
        {
        }
    }
}
