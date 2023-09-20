using Juice.Audit.Commands;
using Juice.Audit.Domain.AccessLogAggregate;
using Juice.Audit.Domain.DataAuditAggregate;
using MediatR;

namespace Juice.Audit.CommandHandlers
{
    internal class SaveAuditInfoCommandHandler : IRequestHandler<SaveAuditInfoCommand, IOperationResult>
    {
        private readonly IDataAuditRepository _auditRepository;
        private readonly IAccessLogRepository _accessLogRepository;

        public SaveAuditInfoCommandHandler(IDataAuditRepository auditRepository, IAccessLogRepository accessLogRepository)
        {
            _accessLogRepository = accessLogRepository;
            _auditRepository = auditRepository;
        }

        public async Task<IOperationResult> Handle(SaveAuditInfoCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.DataAuditEntries?.Any() ?? false)
                {
                    await _auditRepository.AddRangeAsync(request.DataAuditEntries, cancellationToken);
                }

                return await _accessLogRepository.AddAsync(request.AccessLog, cancellationToken);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
