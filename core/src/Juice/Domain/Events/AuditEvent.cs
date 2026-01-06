
namespace Juice.Domain.Events
{

    public record AuditEvent : DataEvent
    {
        public AuditEvent(string name) : base(name)
        {
        }
        public override bool IsAudit => true;

    }

    /// <summary>
    /// Generic data event for entity, use for audit
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public record AuditEvent<T> : AuditEvent
    {
        public AuditEvent(string name) : base(name)
        {
        }
    }

}
