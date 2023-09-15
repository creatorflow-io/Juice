using MediatR;

namespace Juice.Audit.Domain.Events
{
    public class AccessLogCreatedDomainEvent : INotification
    {
        public Guid RecordId { get; private set; }

        public AccessLogCreatedDomainEvent(Guid recordId)
        {
            RecordId = recordId;
        }
    }
}
