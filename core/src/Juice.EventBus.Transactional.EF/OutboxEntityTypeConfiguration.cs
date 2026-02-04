using Juice.EventBus.Delivery;
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

            builder.Property(e => e.TransactionId)
                .HasMaxLength(64);

            builder.Property(e => e.TenantId)
                .HasMaxLength(64);

            builder.Property(e => e.EventTypeName)
                .HasMaxLength(256)
                .IsRequired();

            builder.HasMany(e => e.Deliveries)
                .WithOne(d => d.OutboxEvent)
                .HasForeignKey(d => d.EventId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.TransactionId)
                ;
        }
    }

}
