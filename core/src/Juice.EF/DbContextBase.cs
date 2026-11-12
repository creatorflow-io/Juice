using System.Reflection;
using System.Security.Claims;
using Juice.Domain.Events;
using Juice.EF.Extensions;
using Juice.Measurement;
using Juice.MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.EF
{

    public abstract partial class DbContextBase : UnitOfWork,
        ISchemaDbContext, IAuditableDbContext, IResettableService
    {
        #region Schema context
        public string? Schema { get; protected set; }
        #endregion

        #region Auditable context

        public virtual string? TenantId { get; protected set; }
        public virtual Type? AuditEventType => typeof(AuditEvent<>);
        public virtual Type? DataEventType(string name)
        {
            return name switch
            {
                nameof(DataEvents.Inserted) => typeof(DataInserted<>),
                nameof(DataEvents.Modified) => typeof(DataModified<>),
                nameof(DataEvents.Deleted) => typeof(DataDeleted<>),
                _ => null
            };
        }
        public virtual string? User => UserPrincipal?.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == ClaimTypes.Name)?.Value;
        public ClaimsPrincipal? UserPrincipal { get; protected set; }
        public List<DataEvent> PendingDataEvents { get; set; } = new List<DataEvent>();
        public List<AuditEntry> PendingAuditEntries { get; set; } = new List<AuditEntry>();

        public ITimeTracker? TimeTracker { get; protected set; }

        #endregion

        protected IMediator? _mediator;

        protected ILogger? _logger;

        protected DbOptions? _options;

        /// <summary>
        /// Please call <c>Create(IServiceProvider serviceProvider)</c> directly in your constructor
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
            UserPrincipal = httpContextAccessor?.HttpContext?.User;

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
            catch
            {
            }

            _options = serviceProvider.GetService(typeof(DbOptions<>).MakeGenericType(GetType())) as DbOptions;
            if (_options == null)
            {
                // Try to get from IOptions if DbOptions is registered as options
                var optionsType = typeof(IOptions<>).MakeGenericType(typeof(DbOptions<>).MakeGenericType(GetType()));
                var optionsWrapper = serviceProvider.GetService(optionsType);
                if (optionsWrapper != null)
                {
                    var valueProperty = optionsType.GetProperty("Value");
                    if (valueProperty != null)
                    {
                        _options = valueProperty.GetValue(optionsWrapper) as DbOptions;
                    }
                }
            }
            Schema = _options?.Schema;
            if (_options?.EnableTimeTracking ?? false)
            {
                TimeTracker = serviceProvider.GetService<ITimeTracker>();
            }
        }

        public virtual void SetUser(ClaimsPrincipal? user)
        {
            UserPrincipal = user;
        }

        protected abstract void ConfigureModel(ModelBuilder modelBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureModel(modelBuilder);
            modelBuilder.ConfigureExpandableEntities(this);
            modelBuilder.ConfigureAuditableEntities();
        }


        private HashSet<EntityEntry> _pendingRefreshEntities = [];

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

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.TrackingChanges(_logger);

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
            catch
            {
                ForgetAuditEntriesOfFailedSave();
                throw;
            }
            finally
            {
                await this.DispatchEventsAsync(_mediator, _logger);
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.TrackingChanges(_logger);
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
            catch
            {
                ForgetAuditEntriesOfFailedSave();
                throw;
            }
            finally
            {
                this.DispatchEventsAsync(_mediator, _logger).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// The changes of a failed save were not made, so their audit events must not go out. A managed context keeps
        /// its entries: earlier saves of the transaction are among them, and it reports them only if it commits.
        /// </summary>
        private void ForgetAuditEntriesOfFailedSave()
        {
            if (!IsManaged)
            {
                PendingAuditEntries.Clear();
            }
        }

        #region UnitOfWork

        protected override ILogger? CommitLogger => _logger ?? base.CommitLogger;

        public override async Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
        {
            bool committed;
            try
            {
                committed = await base.CommitTransactionAsync(transactionId, token);
            }
            catch (Exception ex) when (ex is TransactionCommitFailedException || ex is OperationCanceledException)
            {
                // The commit was tried and failed: nothing to reload, possibly on a broken connection. (A wrong transaction id throws before any
                // commit, and the transaction that will commit keeps its entries.)
                _pendingRefreshEntities.Clear();
                throw;
            }
            if (committed)
            {
                // Before the announcement: handlers see the reloaded entities, and entries a handler's own transaction adds wait for its commit.
                await RefreshPendingEntriesAsync(transactionId);
                await AnnounceCommitAsync(transactionId);
            }
            return committed;
        }

        /// <summary>Reloads entities whose database-side values the transaction changed. Logged, never thrown: the data is committed.</summary>
        private async Task RefreshPendingEntriesAsync(Guid transactionId)
        {
            if (_pendingRefreshEntities.Count == 0)
            {
                return;
            }
            var entries = _pendingRefreshEntities.ToList();
            _pendingRefreshEntities.Clear();
            foreach (var entry in entries)
            {
                // One by one, so one entity failing to reload doesn't leave the others stale.
                try
                {
                    await entry.ReloadAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Reloading {EntityType} after transaction {TransactionId} failed: {Message}",
                        entry.Metadata.DisplayName(), transactionId, ex.Message);
                }
            }
        }

        /// <summary>The data is committed, so a failing handler must not fail the command.</summary>
        private async Task AnnounceCommitAsync(Guid transactionId)
        {
            if (_mediator == null)
            {
                return;
            }
            try
            {
                await _mediator.Publish(new TransactionCommitted(transactionId));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Transaction {TransactionId} committed, but announcing it failed: {Message}",
                    transactionId, ex.Message);
            }
        }

        #endregion

        #region Pooling

        /// <summary>
        /// Clears what belongs to one user of the context, on every return to the pool, before anyone else can get it.
        /// Override it to clear state of your own. <c>Schema</c>, <c>_options</c> and <c>_logger</c> are config-derived and
        /// stable across requests: they stay, so pooled contexts don't require <c>ConfigureServices()</c> per use.
        /// </summary>
        protected virtual void OnResetState()
        {
            _mediator = null;
            UserPrincipal = null;
            TimeTracker = null;
            _pendingRefreshEntities.Clear();
            PendingAuditEntries.Clear();
            PendingDataEvents.Clear();
            ResetUnitOfWorkState();
        }

        /// <summary>
        /// EF Core's pool calls this when the context comes back, then makes it available again. It ends this use of the
        /// context, so it is not for clearing state during one: see <see cref="OnResetState"/>.
        /// </summary>
        public virtual void ResetState()
        {
            OnResetState();
            EFCoreReset.Sync(this);
        }

        /// <inheritdoc cref="ResetState"/>
        public virtual async Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            OnResetState();
            await EFCoreReset.Async(this, cancellationToken);
        }

        /// <summary>
        /// DbContext's own reset: the change tracker, the DbSets, the database facade with its connection and transaction, and
        /// every service of the provider. Declaring <see cref="IResettableService"/> here replaces it, and C# has no way to
        /// call a base class's explicit interface method, so it is found through the interface map. Without it, a pooled
        /// context keeps everything it tracked and any open transaction, for the next user.
        /// </summary>
        private static class EFCoreReset
        {
            private static readonly InterfaceMapping Map = typeof(DbContext).GetInterfaceMap(typeof(IResettableService));

            public static readonly Action<DbContext> Sync =
                Target(nameof(IResettableService.ResetState)).CreateDelegate<Action<DbContext>>();

            public static readonly Func<DbContext, CancellationToken, Task> Async =
                Target(nameof(IResettableService.ResetStateAsync)).CreateDelegate<Func<DbContext, CancellationToken, Task>>();

            private static MethodInfo Target(string name)
                => Map.TargetMethods[Array.FindIndex(Map.InterfaceMethods, m => m.Name == name)];
        }

        #endregion
    }
}
