using System.Linq.Expressions;
using System.Security.Claims;
using Juice.Domain;
using Juice.EF.Extensions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EF
{
    public abstract partial class DbContextBase : DbContext,
        ISchemaDbContext, IAuditableDbContext, IUnitOfWork
    {
        #region Schema context
        public string? Schema { get; protected set; }
        #endregion

        #region Auditable context
        public string? User { get; protected set; }

        public List<AuditEntry>? PendingAuditEntries { get; protected set; }

        #endregion

        protected IMediator? _mediator;

        protected ILogger? _logger;

        protected DbOptions? _options;

        /// <summary>
        /// Please call <c>ConfigureServices(IServiceProvider serviceProvider)</c> directly in your constructor
        /// <para>or inside <c>IDbContextFactory.CreateDbContext()</c> if you are using PooledDbContextFactory</para>
        /// <para>to init internal services</para>
        /// </summary>
        /// <param name="options"></param>
        public DbContextBase(DbContextOptions options)
            : base(options)
        {

        }

        public virtual void ConfigureServices(IServiceProvider serviceProvider)
        {
            var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
            User = httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;

            if (_logger == null)
            {
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                _logger = loggerFactory != null ? loggerFactory.CreateLogger(GetType()) : null;
                if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
                {
                    _logger?.LogDebug("Logger initialized for {type}", GetType().Name);
                }
            }
            try
            {
                _mediator = serviceProvider.GetService<IMediator>();
            }
            catch (Exception ex)
            {
            }

            _options = serviceProvider.GetService(typeof(DbOptions<>).MakeGenericType(GetType())) as DbOptions;
            Schema = _options?.Schema;
        }

        protected abstract void ConfigureModel(ModelBuilder modelBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureDynamicExpandableEntities(this);
            modelBuilder.ConfigureAuditableEntities();
            ConfigureModel(modelBuilder);
        }


        private HashSet<EntityEntry> _pendingRefreshEntities = new HashSet<EntityEntry>();

        private void ProcessingRefreshEntries(HashSet<EntityEntry>? entities)
        {
            if (entities == null) { return; }
            if (this.HasActiveTransaction)
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
        private void ProcessingChanges()
        {
            if (PendingAuditEntries == null)
            { return; }
            if (!this.HasActiveTransaction)
            {
                _mediator.DispatchDataChangeEventsAsync(this).GetAwaiter().GetResult();
            }
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            //var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            this.SetAuditInformation(_logger);
            PendingAuditEntries = _mediator != null ? this.TrackingChanges(_logger)?.ToList() : default;

            try
            {
                await _mediator.DispatchDomainEventsAsync(this);
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
                ProcessingChanges();
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.SetAuditInformation(_logger);
            PendingAuditEntries = _mediator != null ? this.TrackingChanges(_logger)?.ToList() : default;
            try
            {
                _mediator.DispatchDomainEventsAsync(this).GetAwaiter().GetResult();
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
                ProcessingChanges();
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            // cleanup self services and data
            _logger?.LogDebug(GetType().Name + " is disposing...");
            _options = null;
            Schema = null;
            User = null;
            _commitedTransactionId = null;
            _mediator = null;
            _logger = null;
            _pendingRefreshEntities.Clear();
            PendingAuditEntries = null;
        }

        #region UnitOfWork

        public bool HasActiveTransaction
            => Database.CurrentTransaction != null
            && Database.CurrentTransaction.TransactionId != _commitedTransactionId;

        private Guid? _commitedTransactionId;

        public async Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
        {
            if (transactionId == _commitedTransactionId)
            {
                return false;
            }
            var transaction = Database.CurrentTransaction;
            if (transaction == null) { throw new ArgumentNullException(nameof(transaction)); }

            if (transaction.TransactionId != transactionId) { throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current"); }

            try
            {
                await SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                return true;
            }
            catch
            {
                RollbackTransaction();
                throw;
            }
            finally
            {
                _commitedTransactionId = transaction.TransactionId;

                if (_pendingRefreshEntities != null)
                {
                    await _pendingRefreshEntities.RefreshEntriesAsync();
                }
                await _mediator.DispatchDataChangeEventsAsync(this);
            }
        }

        private void RollbackTransaction()
        {
            try
            {
                this.GetCurrentTransaction()?.Rollback();
            }
            finally
            {

            }
        }

        public virtual async Task<IOperationResult<T>> AddAndSaveAsync<T>(T entity, CancellationToken token = default)
            where T : class
        {
            try
            {
                var entry = Set<T>().Add(entity);
                await SaveChangesAsync(token);
                return OperationResult.Result(entry.Entity);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed<T>(ex);
            }
        }

        public virtual async Task<IOperationResult> AddAndSaveAsync<T>(IEnumerable<T> entities, CancellationToken token = default)
            where T : class
        {
            try
            {
                Set<T>().AddRange(entities);
                await SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public virtual async Task<IOperationResult> DeleteAsync<T>(T entity, CancellationToken token = default)
            where T : class
        {
            try
            {
                Set<T>().Remove(entity);
                await SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public virtual async Task<IOperationResult> UpdateAsync<T>(T entity, CancellationToken token = default)
            where T : class
        {
            try
            {
                var tracked = Entry(entity).State != EntityState.Detached;
                if (!tracked)
                {
                    Set<T>().Update(entity);
                }
                await SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public virtual async Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
            where T : class
        {
            return await Set<T>().FirstOrDefaultAsync(predicate, token);
        }

        public IQueryable<T> Query<T>()
            where T : class
            => Set<T>();
        #endregion

    }
}
