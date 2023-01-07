using System.ComponentModel.DataAnnotations.Schema;

namespace Juice.Domain.AggregatesModel
{
    public class AuditAggregrateRoot<TKey, TNotification> : AuditEntity<TKey>, IAggregrateRoot<TNotification>
        where TKey : IEquatable<TKey>
    {
        [NotMapped]
        public IList<TNotification> DomainEvents { get; } = new List<TNotification>();
    }
}
