using System.Security.Claims;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.MultiTenant.EF
{
    public class TenantStoreDbContext<TTenantInfo> : EFCoreStoreDbContext<TTenantInfo>, ISchemaDbContext, IAuditableDbContext
        where TTenantInfo : class, IDynamic, IAuditable, ITenantInfo, new()
    {
        #region Audit/Schema
        public string? Schema { get; protected set; }
        public string? User =>
            _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
        public IEnumerable<IDataEventHandler>? AuditHandlers { get; set; }
        #endregion

        private IHttpContextAccessor? _httpContextAccessor;
        private ILogger? _logger;

        public TenantStoreDbContext(
            IServiceProvider serviceProvider, DbContextOptions<TenantStoreDbContext<TTenantInfo>> options) : base(options)
        {
            var dbOptions = serviceProvider.GetService<DbOptions<TenantStoreDbContext<TTenantInfo>>>();
            Schema ??= dbOptions?.Schema ?? "App";
            _httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TTenantInfo>(entity =>
            {
                entity.ToTable(nameof(Tenant), Schema);

                new DynamicConfiguration<TTenantInfo, string>().Configure(entity);
                entity.Property(ti => ti.Id).HasMaxLength(Constants.TenantIdMaxLength);

                entity.Property(ti => ti.Identifier).HasMaxLength(Constants.TenantIdentifierMaxLength);
                entity.HasIndex(ti => ti.Identifier).IsUnique();
            });

        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.SetAuditInformation();

            var changes = this.TrackingChanges();
            try
            {
                using (var transaction = Database.BeginTransaction())
                {
                    var (affects, refeshEntries) = this.TryUpdateDynamicPropertyAsync(_logger).GetAwaiter().GetResult();
                    if (this.HasUnsavedChanges())
                    {
                        affects = base.SaveChanges(acceptAllChangesOnSuccess);
                    }
                    refeshEntries.RefreshEntriesAsync().GetAwaiter().GetResult();
                    transaction.Commit();
                    return affects;
                }
            }
            finally
            {
                if (changes != null)
                {
                    this.NotificationChanges(changes);
                }
            }
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            this.SetAuditInformation();
            //this.EnforceMultiTenant(); //enforce mutitenant be must after audit

            var changes = this.TrackingChanges();

            try
            {
                using (var transaction = Database.BeginTransaction())
                {
                    var (affects, refeshEntries) = await this.TryUpdateDynamicPropertyAsync(_logger);
                    if (this.HasUnsavedChanges())
                    {
                        affects = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                    }
                    await refeshEntries.RefreshEntriesAsync();
                    transaction.Commit();
                    return affects;
                }
            }
            finally
            {
                if (changes != null)
                {
                    this.NotificationChanges(changes);
                }
            }
        }
    }

    /// <summary>
    /// TenantStoreDbContext for migration
    /// </summary>
    public class TenantStoreDbContextWrapper : TenantStoreDbContext<Tenant>
    {
        public TenantStoreDbContextWrapper(IServiceProvider serviceProvider, DbContextOptions<TenantStoreDbContext<Tenant>> options) : base(serviceProvider, options)
        {
        }
    }
}
