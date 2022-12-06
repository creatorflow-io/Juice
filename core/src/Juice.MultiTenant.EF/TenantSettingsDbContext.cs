using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.EF;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.EF
{
    public class TenantSettingsDbContext : DbContext, ISchemaDbContext, IMultiTenantDbContext
    {
        #region Finbuckle
        public ITenantInfo? TenantInfo { get; internal set; }
        public TenantMismatchMode TenantMismatchMode { get; set; } = TenantMismatchMode.Throw;

        public TenantNotSetMode TenantNotSetMode { get; set; } = TenantNotSetMode.Throw;
        #endregion
        public string? Schema { get; protected set; }

        public DbSet<TenantSettings> Settings { get; set; }

        public TenantSettingsDbContext(
            DbOptions<TenantSettingsDbContext> dbOptions,
            DbContextOptions<TenantSettingsDbContext> options,
            ITenantInfo? tenantInfo = null) : base(options)
        {
            Schema ??= dbOptions?.Schema ?? "App";
            TenantInfo = tenantInfo;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // If necessary call the base class method.
            // Recommended to be called first.
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TenantSettings>(entity =>
            {
                entity.ToTable(nameof(TenantSettings), Schema);
                entity.Property(p => p.Key).HasMaxLength(Constants.ConfigurationKeyMaxLength);
                entity.IsMultiTenant();
                entity.HasKey(p => p.Id);
                entity.HasIndex("Key", "TenantId").IsUnique();
            });
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.EnforceMultiTenant();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            this.EnforceMultiTenant();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
