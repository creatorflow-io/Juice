using System.Linq.Expressions;
using Juice.Audit.Domain.AccessLogAggregate;

namespace Juice.Audit.Tests.Host.Mockservices
{
    internal class AccessLogRepository : IAccessLogRepository
    {
        private readonly ILogger<AccessLogRepository> _logger;
        public AccessLogRepository(ILogger<AccessLogRepository> logger)
        {
            _logger = logger;
        }
        public Task<IOperationResult<AccessLog>> AddAsync(AccessLog entity, CancellationToken token)
        {
            _logger.LogInformation("AccessRecord was added!");
            return Task.FromResult((IOperationResult<AccessLog>)OperationResult<AccessLog>.Result(entity));
        }
        public Task<IOperationResult> DeleteAsync(AccessLog entity, CancellationToken token) => throw new NotImplementedException();
        public Task<AccessLog?> FindAsync(Expression<Func<AccessLog, bool>> predicate, CancellationToken token) => throw new NotImplementedException();
        public Task<IOperationResult> UpdateAsync(AccessLog entity, CancellationToken token) => throw new NotImplementedException();
    }
}
