using System.Linq.Expressions;
using Juice.Audit.Domain.DataAuditAggregate;

namespace Juice.Audit.Tests.Host.Mockservices
{
    internal class DataAuditRepository : IDataAuditRepository
    {
        private readonly ILogger<DataAuditRepository> _logger;
        public DataAuditRepository(ILogger<DataAuditRepository> logger)
        {
            _logger = logger;
        }
        public Task<IOperationResult<DataAudit>> AddAsync(DataAudit entity, CancellationToken token)
        {
            _logger.LogInformation("AuditEntry was added!");
            return Task.FromResult((IOperationResult<DataAudit>)OperationResult<DataAudit>.Result(entity));
        }
        public Task AddRangeAsync(IEnumerable<DataAudit> auditEntries, CancellationToken token)
        {
            _logger.LogInformation("AuditEntries were added! {0}", auditEntries.Count());
            return Task.CompletedTask;
        }
        public Task<IOperationResult> DeleteAsync(DataAudit entity, CancellationToken token) => throw new NotImplementedException();
        public Task<DataAudit?> FindAsync(Expression<Func<DataAudit, bool>> predicate, CancellationToken token) => throw new NotImplementedException();
        public Task<IOperationResult> UpdateAsync(DataAudit entity, CancellationToken token) => throw new NotImplementedException();
    }
}
