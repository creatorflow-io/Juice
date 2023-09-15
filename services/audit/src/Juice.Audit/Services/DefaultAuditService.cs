using Juice.Audit.Domain.AccessLogAggregate;
using Juice.Audit.Domain.DataAuditAggregate;
using Juice.Audit.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Audit.Services
{
    internal class DefaultAuditService : IAuditService
    {
        private readonly IDataAuditRepository _auditRepository;
        private readonly IAccessLogRepository _accessLogRepository;
        private readonly IAuditContextAccessor _auditContextAccessor;

        private readonly ILogger _logger;
        private readonly IMediator _mediator;

        public DefaultAuditService(
            IDataAuditRepository auditRepository,
            IAccessLogRepository accessLogRepository,
            IAuditContextAccessor auditContextAccessor,
            IMediator mediator,
            ILogger<DefaultAuditService> logger)
        {
            _auditRepository = auditRepository;
            _accessLogRepository = accessLogRepository;
            _auditContextAccessor = auditContextAccessor;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<IOperationResult> CommitAuditInformationAsync(CancellationToken token)
        {
            try
            {
                var auditContext = _auditContextAccessor.AuditContext;
                if (auditContext == null)
                {
                    return OperationResult.Failed("AuditContext was not initialized!");
                }

                if (auditContext.AccessRecord != null)
                {
                    var accessRecordResult = await _accessLogRepository.AddAsync(auditContext.AccessRecord, token);

                    if (!accessRecordResult.Succeeded)
                    {
                        return accessRecordResult;
                    }

                    if (accessRecordResult.Data != null)
                    {
                        await _mediator.Publish(new AccessLogCreatedDomainEvent(accessRecordResult.Data.Id), token);
                        foreach (var auditEntry in auditContext.AuditEntries)
                        {
                            auditEntry.SetAccessId(accessRecordResult.Data.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("AccessRecord was not added!");
                    }
                }
                else
                {
                    _logger.LogInformation("AccessRecord was not initialized!");
                }

                await _auditRepository.AddRangeAsync(auditContext.AuditEntries, token);

                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
