using Juice.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public class IntegrationEventLogContext : DbContext, ISchemaDbContext
    {
        public string? Schema { get; private set; }
        public IntegrationEventLogContext(IntegrationEventLogContextOptions dbOptions,
            DbContextOptions<IntegrationEventLogContext> options) : base(options)
        {
            Schema = dbOptions.Schema;
        }

        public IntegrationEventLogContext(IOptions<IntegrationEventLogContextOptions> dbOptions,
            DbContextOptions<IntegrationEventLogContext> options) : base(options)
        {
            Schema = dbOptions.Value.Schema;
        }

        public DbSet<IntegrationEventLogEntry> IntegrationEventLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<IntegrationEventLogEntry>(ConfigureIntegrationEventLogEntry);
        }

        private void ConfigureIntegrationEventLogEntry(EntityTypeBuilder<IntegrationEventLogEntry> builder)
        {
            builder.ToTable("IntegrationEventLog", Schema);

            builder.HasKey(e => e.EventId);

            builder.Property(e => e.EventId)
                .IsRequired();

            builder.Property(e => e.Content)
                .IsRequired();

            builder.Property(e => e.CreationTime)
                .IsRequired();

            builder.Property(e => e.State)
                .IsRequired();

            builder.Property(e => e.TimesSent)
                .IsRequired();

            builder.Property(e => e.EventTypeName)
                .IsRequired();

        }
    }
}
