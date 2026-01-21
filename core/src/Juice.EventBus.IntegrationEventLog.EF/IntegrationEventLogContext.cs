using Juice.EF;
using Juice.EventBus.Transactional.EF;
using Microsoft.EntityFrameworkCore;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public class IntegrationEventLogContext : DbContext, ISchemaDbContext, IOutboxContext
    {
        public string? Schema { get; private set; }

        public IntegrationEventLogContext(DbOptions<IntegrationEventLogContext> dbOptions,
            DbContextOptions<IntegrationEventLogContext> options) : base(options)
        {
            Schema = dbOptions.Schema;
        }

        public DbSet<OutboxEvent> Outbox { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            new IntegrationEventLogEntityTypeConfiguration(Schema).Configure(builder.Entity<OutboxEvent>());
        }
    }
}
