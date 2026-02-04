using Juice.EventBus.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Juice.EventBus.Transactional.EF
{
    public interface IOutboxContext
    {
        DbSet<OutboxEvent> Outbox { get; }
        DbSet<OutboxDelivery> OutboxDeliveries { get; }
    }
}
