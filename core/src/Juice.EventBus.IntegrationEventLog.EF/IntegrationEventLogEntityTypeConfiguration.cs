using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public class IntegrationEventLogEntityTypeConfiguration : IEntityTypeConfiguration<IntegrationEventLogEntry>
    {
        private readonly string? Schema;
        public IntegrationEventLogEntityTypeConfiguration(string? schema = null)
        {
            Schema = schema;
        }
        public void Configure(EntityTypeBuilder<IntegrationEventLogEntry> builder)
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
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(e => e.TransactionId)
                .HasMaxLength(64);

            builder.HasIndex(e => e.TransactionId)
                ;

            builder.HasIndex(
                nameof(IntegrationEventLogEntry.State),
                nameof(IntegrationEventLogEntry.ModificationTime),
                nameof(IntegrationEventLogEntry.TimesSent)
                )
                .HasDatabaseName("IX_IntegrationEventLog_Recovery")
                .HasFilter("[ModificationTime] IS NOT NULL")
                ;
        }
    }
}
