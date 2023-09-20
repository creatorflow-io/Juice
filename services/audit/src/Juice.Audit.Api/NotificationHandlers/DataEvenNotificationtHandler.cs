using Juice.EF;
using MediatR;
using Newtonsoft.Json;

namespace Juice.Audit.Api.NotificationHandlers
{
    internal class DataEvenNotificationtHandler : INotificationHandler<DataEvent>
    {
        private IAuditContextAccessor _auditContextAccessor;

        public DataEvenNotificationtHandler(IAuditContextAccessor auditContextAccessor)
        {
            _auditContextAccessor = auditContextAccessor;
        }

        public Task Handle(DataEvent notification, CancellationToken cancellationToken)
        {
            var auditRecord = notification.AuditRecord;
            if (auditRecord != null)
            {
                _auditContextAccessor.AuditContext?.AddAuditEntries(new Domain.DataAuditAggregate.DataAudit(
                    auditRecord.User,
                    DateTimeOffset.UtcNow,
                    notification.Name,
                    auditRecord.Database,
                    auditRecord.Schema,
                    auditRecord.Table,
                    JsonConvert.SerializeObject(auditRecord.KeyValues),
                    JsonConvert.SerializeObject(new { auditRecord.OriginalValues, auditRecord.CurrentValues }),
                    _auditContextAccessor.AuditContext?.AccessRecord?.TraceId
                    ));
            }
            return Task.CompletedTask;
        }
    }
}
