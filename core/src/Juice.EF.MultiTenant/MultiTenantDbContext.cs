using System.Data;
using System.Security.Claims;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.EF.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EF.MultiTenant
{
    public abstract class MultiTenantDbContext : DbContext,
        ISchemaDbContext, IAuditableDbContext, IUnitOfWork, IMultiTenantDbContext
    {
        #region Finbuckle
        public ITenantInfo? TenantInfo { get; internal set; }
        public TenantMismatchMode TenantMismatchMode { get; set; } = TenantMismatchMode.Throw;

        public TenantNotSetMode TenantNotSetMode { get; set; } = TenantNotSetMode.Throw;
        #endregion

        #region Schema context
        public string? Schema { get; protected set; }
        #endregion
        #region Auditable context
        public string? User => _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;

        private IHttpContextAccessor? _httpContextAccessor;
        #endregion

        #region Audit events
        public IEnumerable<IDataEventHandler>? AuditHandlers { get; set; }

        #endregion


        private ILogger? _logger;
        private IServiceProvider _serviceProvider;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly DbOptions? _options;

        public MultiTenantDbContext(IServiceProvider serviceProvider, DbContextOptions options)
            : base(options)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
            if (_httpContextAccessor?.HttpContext != null)
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(_httpContextAccessor.HttpContext.RequestAborted);
            }
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            _logger = loggerFactory != null ? loggerFactory.CreateLogger(GetType()) : null;
            _logger?.LogInformation("Logger initialized for {type}", GetType().Name);
            try
            {
                AuditHandlers = serviceProvider.GetServices<IDataEventHandler>();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"[DbContextBase] failed to receive audit handlers. {ex.Message}");
            }

            _options = serviceProvider.GetService(typeof(DbOptions<>).MakeGenericType(GetType())) as DbOptions;
            Schema = _options?.Schema;

            TenantInfo = serviceProvider.GetService<ITenantInfo>();
        }


        protected abstract void ConfigureModel(ModelBuilder modelBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureDynamicExpandableEntities(this);

            ConfigureModel(modelBuilder);

            var collections = _serviceProvider
                .GetServices<IModelConfiguration>()
                .Where(c => typeof(ModelConfigurationBase<>).MakeGenericType(GetType()).IsAssignableFrom(c.GetType()));
            foreach (var callback in collections)
            {
                callback.OnModelCreating(modelBuilder);
            }

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

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            this.SetAuditInformation(_logger);
            this.EnforceMultiTenant();
            var changes = this.TrackingChanges(_logger);

            try
            {
                if (_options != null && _options.JsonPropertyBehavior == JsonPropertyBehavior.UpdateALL)
                {
                    return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cts.Token);
                }

                var (affects, refeshEntries) = await this.TryUpdateDynamicPropertyAsync(_logger);
                if (this.HasUnsavedChanges())
                {
                    affects = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cts.Token);
                }

                ProcessingRefreshEntries(refeshEntries);
                return affects;

            }
            finally
            {
                ProcessingChanges(changes);
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.SetAuditInformation(_logger);
            this.EnforceMultiTenant();
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

        #region UnitOfWork

        private IDbContextTransaction _currentTransaction;
        public IDbContextTransaction GetCurrentTransaction() => _currentTransaction;
        public bool HasActiveTransaction => _currentTransaction != null;
        private Guid? _commitedTransactionId;

        public async Task<IDbContextTransaction?> BeginTransactionAsync()
        {
            if (_currentTransaction != null) { return default; }

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
}
