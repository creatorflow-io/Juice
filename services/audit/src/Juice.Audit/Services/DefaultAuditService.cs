using Juice.Audit.Commands;
using Juice.Audit.Domain.AccessLogAggregate;
using Juice.Audit.Domain.DataAuditAggregate;
using MediatR;

namespace Juice.Audit.Services
{
    internal class DefaultAuditService : IAuditService
    {

        private readonly IMediator _mediator;

        public DefaultAuditService(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<IOperationResult> PersistAuditInformationAsync(AccessLog accessLog, DataAudit[] auditEntries, CancellationToken token)
        {
            return await _mediator.Send(new SaveAuditInfoCommand(accessLog, auditEntries), token);
        }
    }
}
