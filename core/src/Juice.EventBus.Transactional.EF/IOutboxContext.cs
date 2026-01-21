using Microsoft.EntityFrameworkCore;

namespace Juice.EventBus.Transactional.EF
{
    public interface IOutboxContext
    {
        DbSet<OutboxEvent> Outbox { get; }
    }
}
