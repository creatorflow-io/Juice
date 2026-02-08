using Microsoft.EntityFrameworkCore;

namespace Juice.Messaging.Outbox.EF
{
    public interface IOutboxContext
    {
        DbSet<OutboxEvent> Outbox { get; }
        DbSet<OutboxDelivery> OutboxDeliveries { get; }
    }
}
