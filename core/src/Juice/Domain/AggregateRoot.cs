using System.ComponentModel.DataAnnotations.Schema;

namespace Juice.Domain
{
    /// <summary>
    /// Default aggregrate root
    /// </summary>
    /// <typeparam name="TNotification"></typeparam>
    public abstract class AggregateRoot<TNotification> : IAggregateRoot<TNotification>,
        IValidatable
    {
        [NotMapped]
        public IList<TNotification> DomainEvents { get; } = [];

        [NotMapped]
        public IList<string> ValidationErrors { get; } = [];
    }

    /// <summary>
    /// Use for aggregrate root that mapped to single auditable entity
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TNotification"></typeparam>
    public abstract class AuditAggregrateRoot<TKey, TNotification> : AuditEntity<TKey>, IAggregateRoot<TNotification>, IValidatable
        where TKey : IEquatable<TKey>
    {
        protected AuditAggregrateRoot(TKey id, string name) : base(id, name)
        {
        }

        [NotMapped]
        public IList<TNotification> DomainEvents { get; } = new List<TNotification>();


        [NotMapped]
        public IList<string> ValidationErrors { get; } = new List<string>();
    }
}
