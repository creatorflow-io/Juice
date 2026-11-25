
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

        /// <summary>
        /// The type of the DbContext that made the change, null when the event was not raised by one. A handler that writes to the
        /// outbox of the change's transaction resolves <c>IOutboxService&lt;TContext&gt;</c> for it.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public Type? ContextType { get; private set; }

        public AuditEvent SetContextType(Type? contextType)
        {
            ContextType = contextType;
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
