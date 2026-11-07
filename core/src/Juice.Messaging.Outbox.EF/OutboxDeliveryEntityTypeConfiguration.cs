using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.Messaging.Outbox.EF
{
    public class OutboxDeliveryEntityTypeConfiguration : IEntityTypeConfiguration<OutboxDelivery>
    {
        private readonly string? Schema;
        private readonly bool _bracketQuotes;

        /// <summary>Writes the partial-index filters for SQL Server.</summary>
        public OutboxDeliveryEntityTypeConfiguration(string? schema = null)
            : this(schema, providerName: null)
        {
        }

        /// <summary>
        /// Writes the partial-index filters for <paramref name="providerName"/> (a context's <c>Database.ProviderName</c>):
        /// <c>[State]</c> for SQL Server or no provider, <c>"State"</c> for any other. The filters use the properties' column
        /// names, so they follow a naming convention set on the context.
        /// </summary>
        public OutboxDeliveryEntityTypeConfiguration(string? schema, string? providerName)
        {
            Schema = schema;
            _bracketQuotes = providerName is null
                || providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);
        }

        private string Column(EntityTypeBuilder<OutboxDelivery> builder, string propertyName)
        {
            var column = builder.Metadata.FindProperty(propertyName)!.GetColumnName();
            return _bracketQuotes ? $"[{column}]" : $"\"{column}\"";
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

            var state = Column(builder, nameof(OutboxDelivery.State));

            builder.HasIndex(
                nameof(OutboxDelivery.CreationTime)
                )
                .HasFilter($"{state} = {(int)DeliveryState.NotPublished}")
                .HasDatabaseName("IX_OutboxDeliveries_Pending")
                .HasAnnotation("SqlServer:Include", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                .HasAnnotation("Npgsql:IndexInclude", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                ;

            builder.HasIndex(
                nameof(OutboxDelivery.NextAttemptOn)
                )
                .HasFilter($"{state} = {(int)DeliveryState.Failed} AND {Column(builder, nameof(OutboxDelivery.NextAttemptOn))} IS NOT NULL")
                .HasDatabaseName("IX_OutboxDeliveries_Retry")
                .HasAnnotation("SqlServer:Include", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                .HasAnnotation("Npgsql:IndexInclude", new[] { nameof(OutboxDelivery.EventId), nameof(OutboxDelivery.PublisherKey) })
                ;

            builder.HasIndex(
                nameof(OutboxDelivery.ProcessedOn)
                )
                .HasFilter($"{state} = {(int)DeliveryState.InProgress}")
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
