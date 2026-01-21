using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.EventBus.Transactional.EF
{
    public class OutboxEntityTypeConfiguration : IEntityTypeConfiguration<OutboxEvent>
    {
        private readonly string? Schema;
        public OutboxEntityTypeConfiguration(string? schema = null)
        {
            Schema = schema;
        }
        public void Configure(EntityTypeBuilder<OutboxEvent> builder)
        {
            builder.ToTable("OutboxEvents", Schema);

            builder.HasKey(e => e.EventId);

            builder.Property(e => e.EventId)
                .IsRequired();

            builder.Property(e => e.Payload)
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

            builder.Property(e => e.LastError)
                .HasMaxLength(LengthConstants.ShortDescriptionLength);

            builder.Property(e => e.TransactionId)
                .HasMaxLength(64);

            builder.HasIndex(e => e.TransactionId)
                ;

            builder.HasIndex(
                nameof(OutboxEvent.State),
                nameof(OutboxEvent.ProcessedOn),
                nameof(OutboxEvent.TimesSent)
                )
                .HasDatabaseName("IX_OutboxEvents_Recovery")
                .HasFilter("[ProcessedOn] IS NOT NULL")
                ;
        }
    }

}
