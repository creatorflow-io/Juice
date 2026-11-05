namespace Juice.Domain.Events
{
    /// <summary>
    /// A transaction managed by <c>TransactionBehavior</c> committed. The <see cref="AuditEvent"/>s stamped with its
    /// <see cref="AuditEvent.TransactionId"/> describe changes that are now durable. A transaction that fails publishes
    /// nothing, so a handler that holds those events until this one drops them by default.
    /// </summary>
    public record TransactionCommitted(Guid TransactionId) : DataEvent(nameof(TransactionCommitted));
}
