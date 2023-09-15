using Juice.Domain;
using MediatR;

namespace Juice.Audit.Domain.DataAuditAggregate
{
    public class DataAudit : AggregrateRoot<INotification>
    {
        public Guid Id { get; set; }
        public string? User { get; init; }
        public DateTimeOffset DateTime { get; init; }
        public string Action { get; init; }
        public string Database { get; init; }
        public string Schema { get; init; }
        public string Table { get; init; }
        public string DataChanges { get; init; }
        public Guid? AccessId { get; private set; }

        public void SetAccessId(Guid accessId)
        {
            AccessId = accessId;
        }
    }
}
