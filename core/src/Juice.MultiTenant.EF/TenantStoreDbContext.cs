using System.Data;
using System.Security.Claims;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.MultiTenant.EF
{
    public class TenantStoreDbContext<TTenantInfo> : EFCoreStoreDbContext<TTenantInfo>, ISchemaDbContext, IAuditableDbContext, IUnitOfWork
        where TTenantInfo : class, IDynamic, ITenantInfo, new()
    {
        #region Audit/Schema
        public string? Schema { get; protected set; }
        public string? User =>
            _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
        public IEnumerable<IDataEventHandler>? AuditHandlers { get; set; }
        #endregion

        private IHttpContextAccessor? _httpContextAccessor;
        private ILogger? _logger;
        private readonly DbOptions? _options;

        public TenantStoreDbContext(
            IServiceProvider serviceProvider, DbContextOptions<TenantStoreDbContext<TTenantInfo>> options) : base(options)
        {
            _options = serviceProvider.GetService<DbOptions<TenantStoreDbContext<TTenantInfo>>>();
            Schema ??= _options?.Schema ?? "App";
            _httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
            _logger = serviceProvider.GetService<ILogger<TenantStoreDbContext<TTenantInfo>>>();
            AuditHandlers = serviceProvider.GetServices<IDataEventHandler>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TTenantInfo>(entity =>
            {
                entity.ToTable(nameof(Tenant), Schema);

                entity.MarkAsDynamicExpandable(this);

                entity.MarkAsAuditable();

                entity.Property(ti => ti.Id).HasMaxLength(Constants.TenantIdMaxLength);

                entity.Property(ti => ti.Identifier).HasMaxLength(Constants.TenantIdentifierMaxLength);
                entity.HasIndex(ti => ti.Identifier).IsUnique();
            });

        }

        private HashSet<EntityEntry> _pendingRefreshEntities = new HashSet<EntityEntry>();
        private List<AuditEntry> _pendingAuditEntries = new List<AuditEntry>();

        private void ProcessingRefreshEntries(HashSet<EntityEntry>? entities)
        {
            if (entities == null) { return; }
            if (HasActiveTransaction)
            {
                // Waitting for transaction completed before reload entities
                foreach (var entity in entities)
                {
                    _pendingRefreshEntities.Add(entity);
                }
            }
            else
            {
                entities.RefreshEntriesAsync().GetAwaiter().GetResult();
            }
        }
        private void ProcessingChanges(IEnumerable<AuditEntry>? changes)
        {
            if (changes == null)
            { return; }
            if (!HasActiveTransaction)
            {
                this.NotificationChanges(changes);
            }
            else
            {
                // Waitting for transaction completed before raise data events
                _pendingAuditEntries.AddRange(changes);
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.SetAuditInformation(_logger);

            var changes = this.TrackingChanges(_logger);
            try
            {
                if (_options != null && _options.JsonPropertyBehavior == JsonPropertyBehavior.UpdateALL)
                {
                    return base.SaveChanges(acceptAllChangesOnSuccess);
                }

                var (affects, refeshEntries) = this.TryUpdateDynamicPropertyAsync(_logger).GetAwaiter().GetResult();
                if (this.HasUnsavedChanges())
                {
                    affects = base.SaveChanges(acceptAllChangesOnSuccess);
                }

                ProcessingRefreshEntries(refeshEntries);

                return affects;
            }
            finally
            {
                ProcessingChanges(changes);
            }
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            this.SetAuditInformation(_logger);
            //this.EnforceMultiTenant(); //enforce mutitenant be must after audit

            var changes = this.TrackingChanges(_logger);

            try
            {
                if (_options != null && _options.JsonPropertyBehavior == JsonPropertyBehavior.UpdateALL)
                {
                    return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                }

                var (affects, refeshEntries) = await this.TryUpdateDynamicPropertyAsync(_logger);
                if (this.HasUnsavedChanges())
                {
                    affects = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                }

                ProcessingRefreshEntries(refeshEntries);

                return affects;
            }
            finally
            {
                ProcessingChanges(changes);
            }
        }

        #region UnitOfWork
        private IDbContextTransaction _currentTransaction;
        public IDbContextTransaction GetCurrentTransaction() => _currentTransaction;
        public bool HasActiveTransaction => _currentTransaction != null;
        private Guid? _commitedTransactionId;

        public async Task<IDbContextTransaction?> BeginTransactionAsync()
        {
            if (_currentTransaction != null) return default;

            _commitedTransactionId = default;
            _currentTransaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            return _currentTransaction;
        }

        public async Task CommitTransactionAsync(IDbContextTransaction transaction)
        {
            if (transaction == null) { throw new ArgumentNullException(nameof(transaction)); }
            if (transaction.TransactionId == _commitedTransactionId)
            {
                return;
            }
            if (transaction.TransactionId != _currentTransaction?.TransactionId) { throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current"); }

            try
            {
                await SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                RollbackTransaction();
                throw;
            }
            finally
            {
                _commitedTransactionId = transaction.TransactionId;
                if (_currentTransaction != null)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }
                if (_pendingRefreshEntities != null)
                {
                    await _pendingRefreshEntities.RefreshEntriesAsync();
                }
                this.NotificationChanges(_pendingAuditEntries);
            }
        }

        public void RollbackTransaction()
        {
            try
            {
                _currentTransaction?.Rollback();
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }
            }
        }
        #endregion

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).
                    try
                    {
                        _pendingAuditEntries = null;
                        _pendingRefreshEntities = null;
                    }
                    catch { }
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public override void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            base.Dispose();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
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
