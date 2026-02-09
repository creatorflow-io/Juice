using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace Juice.Messaging.Outbox.EF
{
    public class OutboxEntityTypeConfiguration : IEntityTypeConfiguration<OutboxEvent>
    {
        private readonly string? schema;
        public OutboxEntityTypeConfiguration(string? schema)
        {
            this.schema = schema;
        }
        public void Configure(EntityTypeBuilder<OutboxEvent> builder)
        {
            builder.ToTable("OutboxEvents", schema);

            builder.HasKey(e => e.EventId);

            builder.Property(e => e.EventId)
                .IsRequired();

            builder.Property(e => e.PayloadBytes)
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
            var headerConverter = new ValueConverter<Dictionary<string, object?>, string>(
                        v => JsonConvert.SerializeObject(v),
                        v => JsonConvert.DeserializeObject<Dictionary<string, object?>>(v)!
                    );
            var propertyBuilder = builder.Property(e => e.Headers)
                    .IsRequired()
                    .HasConversion(headerConverter)
                    .HasDefaultValue(new Dictionary<string, object?>());

        }

    }

}
