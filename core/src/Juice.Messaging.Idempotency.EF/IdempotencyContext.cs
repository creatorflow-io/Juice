using Juice.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.Messaging.Idempotency.EF
{

    public class IdempotencyContext : DbContext, ISchemaDbContext
    {
        public string? Schema { get; protected set; }

        public IdempotencyContext(DbOptions<IdempotencyContext> dbOptions,
            DbContextOptions<IdempotencyContext> options) : base(options)
        {
            Schema = dbOptions.Schema;
        }

        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<IdempotencyRecord>(ConfigureClientRequest);
        }

        private void ConfigureClientRequest(EntityTypeBuilder<IdempotencyRecord> builder)
        {
            builder.ToTable("IdempotencyRecords", Schema);

            builder.HasKey(nameof(IdempotencyRecord.Scope), nameof(IdempotencyRecord.Key));

            builder.Property(e => e.Scope)
                .HasMaxLength(LengthConstants.IdentityLength)
                .IsRequired();

            builder.Property(e => e.Key)
                .HasMaxLength(LengthConstants.NameLength)
                .IsRequired();

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.Property(e => e.State)
                .IsRequired();

            builder.Property(e => e.ProcessedBy)
                .HasMaxLength(LengthConstants.NameLength);

        }
    }

}
