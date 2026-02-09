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
        public static void ConfigureOutbox<T>(this T context, ModelBuilder modelBuilder, string? schema = default)
            where T : DbContext, IOutboxContext
        {
            schema ??= (context as ISchemaDbContext)?.Schema;
            new OutboxEntityTypeConfiguration(schema).Configure(modelBuilder.Entity<OutboxEvent>());
            new OutboxDeliveryEntityTypeConfiguration(schema).Configure(modelBuilder.Entity<OutboxDelivery>());
        }
    }
}
