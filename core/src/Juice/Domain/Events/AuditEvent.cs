
namespace Juice.Domain.Events
{

    public record AuditEvent : DataEvent
    {
        public AuditEvent(string name) : base(name)
        {
        }
        public override bool IsAudit => true;

        /// <summary>
        /// The managed transaction this change is part of, null when the change is already durable. Published before
        /// the commit: the change is durable only when <see cref="TransactionCommitted"/> follows with this id.
        /// </summary>
        public Guid? TransactionId { get; private set; }

        public AuditEvent SetTransactionId(Guid? transactionId)
        {
            TransactionId = transactionId;
            return this;
        }
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
