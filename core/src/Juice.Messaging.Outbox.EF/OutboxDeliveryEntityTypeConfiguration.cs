using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.Messaging.Outbox.EF
{
    public class OutboxDeliveryEntityTypeConfiguration : IEntityTypeConfiguration<OutboxDelivery>
    {
        private readonly string? Schema;
        public OutboxDeliveryEntityTypeConfiguration(string? schema = null)
        {
            Schema = schema;
        }
        public void Configure(EntityTypeBuilder<OutboxDelivery> builder)
        {
            builder.ToTable("OutboxDeliveries", Schema);

            builder.HasKey(e => e.DeliveryId);

            builder.Property(e => e.DeliveryId);

            builder.Property(e => e.EventId)
                .IsRequired();

            builder.Property(e => e.PublisherKey)
                .HasMaxLength(LengthConstants.IdentityLength)
                .IsRequired();

            builder.Property(e => e.Destination)
               .HasMaxLength(LengthConstants.IdentityLength)
               .IsRequired();

            builder.Property(e => e.RoutingKey)
               .HasMaxLength(LengthConstants.IdentityLength);

            builder.Property(e => e.State)
                .IsRequired();

            builder.Property(e => e.RetryCount)
                .IsRequired();

            builder.Property(e => e.LastError)
                .HasMaxLength(LengthConstants.ShortDescriptionLength);

            builder.Property(e => e.ProcessedBy)
                .HasMaxLength(LengthConstants.NameLength);

            builder.HasIndex(e => e.PublisherKey)
                ;

            builder.HasIndex(
                nameof(OutboxDelivery.CreationTime)
                )
                .HasFilter($"[State] = {(int)DeliveryState.NotPublished}")
                .HasDatabaseName("IX_OutboxDeliveries_Pending")
                .HasAnnotation("SqlServer:Include", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                .HasAnnotation("Npgsql:IndexInclude", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                ;

            builder.HasIndex(
                nameof(OutboxDelivery.NextAttemptOn)
                )
                .HasFilter($"[State] = {(int)DeliveryState.Failed} AND [NextAttemptOn] IS NOT NULL")
                .HasDatabaseName("IX_OutboxDeliveries_Retry")
                .HasAnnotation("SqlServer:Include", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                .HasAnnotation("Npgsql:IndexInclude", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                ;

            builder.HasIndex(
                nameof(OutboxDelivery.ProcessedOn)
                )
                .HasFilter($"[State] = {(int)DeliveryState.InProgress}")
                .HasDatabaseName("IX_OutboxDeliveries_Recovery")
                .HasAnnotation("SqlServer:Include", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                .HasAnnotation("Npgsql:IndexInclude", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                ;

            // Optional PublisherKey helper index (index intersect), use when we have many publishers
            //builder.HasIndex(
            //    nameof(OutboxDelivery.PublisherKey)
            //    )
            //    .HasDatabaseName("IX_OutboxDeliveries_Publisher")
            //    .HasAnnotation("SqlServer:Include", new[] { nameof(OutboxDelivery.State),
            //        nameof(OutboxDelivery.CreationTime),
            //        nameof(OutboxDelivery.NextAttemptOn),
            //        nameof(OutboxDelivery.ProcessedOn)
            //    })
            //    ;
        }
    }

}
