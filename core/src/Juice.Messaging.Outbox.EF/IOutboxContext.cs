using Juice.EF;
using Microsoft.EntityFrameworkCore;

namespace Juice.Messaging.Outbox.EF
{
    public interface IOutboxContext
    {
        DbSet<OutboxEvent> Outbox { get; }
        DbSet<OutboxDelivery> OutboxDeliveries { get; }
    }

    public static class OutboxContextExtensions
    {
        /// <summary>
        /// Maps the outbox tables. The deliveries' partial-index filters are written for the context's provider and use the
        /// column names, so they follow a naming convention set in the context's options.
        /// </summary>
        public static void ConfigureOutbox<T>(this T context, ModelBuilder modelBuilder, string? schema = default)
            where T : DbContext, IOutboxContext
        {
            schema ??= (context as ISchemaDbContext)?.Schema;
            new OutboxEntityTypeConfiguration(schema).Configure(modelBuilder.Entity<OutboxEvent>());
            new OutboxDeliveryEntityTypeConfiguration(schema, context.Database.ProviderName)
                .Configure(modelBuilder.Entity<OutboxDelivery>());
        }
    }
}
