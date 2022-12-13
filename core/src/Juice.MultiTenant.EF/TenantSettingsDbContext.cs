using System.Data;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.EF;
using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.MultiTenant.EF
{
    public class TenantSettingsDbContext : DbContext, ISchemaDbContext, IMultiTenantDbContext, IUnitOfWork
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

    }
}
