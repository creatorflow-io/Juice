
using Microsoft.EntityFrameworkCore;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public interface IIntegrationEventLogDbContext
    {
        DbSet<IntegrationEventLogEntry> IntegrationEventLogs { get; }
    }
}
