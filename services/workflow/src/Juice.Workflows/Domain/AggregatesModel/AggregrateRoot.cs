using System.ComponentModel.DataAnnotations.Schema;
using Juice.Domain;
using MediatR;

namespace Juice.Workflows.Domain.AggregatesModel
{
    public class AuditAggregrateRoot<TKey> : AuditEntity<TKey>, IAggregrateRoot<INotification>
        where TKey : IEquatable<TKey>
    {
        [NotMapped]
        public IList<INotification> Notifications { get; } = new List<INotification>();
    }
}
