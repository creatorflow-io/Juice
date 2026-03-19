using Microsoft.EntityFrameworkCore;

namespace Juice.Messaging.Outbox.EF
{
    /// <summary>
    /// Standalone outbox <see cref="DbContext"/> for use outside domain transactions.
    /// Implements <see cref="IOutboxContext"/> with the same table schema as <c>OutboxContext</c>
    /// (uses the same <see cref="OutboxContextExtensions.ConfigureOutbox{T}"/> extension).
    /// <para>
    /// <c>IsManaged</c> is always <c>false</c> — <c>SaveEventsAsync</c>
    /// executes immediately as a standalone operation.
    /// </para>
    /// <para>
    /// Tables must be created by running <c>OutboxContext</c> migrations against the target database.
    /// No separate migration project is required.
    /// </para>
    /// </summary>
    public class DefaultOutboxContext : DbContext, IOutboxContext
    {
        public DbSet<OutboxEvent> Outbox { get; set; } = null!;
        public DbSet<OutboxDelivery> OutboxDeliveries { get; set; } = null!;

        public DefaultOutboxContext(DbContextOptions<DefaultOutboxContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            this.ConfigureOutbox(modelBuilder);
        }
    }
}
