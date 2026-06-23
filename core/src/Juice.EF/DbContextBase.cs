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
            finally
            {
                this.DispatchEventsAsync(_mediator, _logger).GetAwaiter().GetResult();
            }
        }

        #region UnitOfWork

        public override async Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
        {
            try
            {
                return await base.CommitTransactionAsync(transactionId, token);
            }
            finally
            {
                if (_pendingRefreshEntities != null)
                {
                    await _pendingRefreshEntities.RefreshEntriesAsync();
                }
            }
        }

        #endregion

        public virtual void ResetState()
        {
            // Per-request state — reset on every pool return
            _mediator = null;
            UserPrincipal = null;
            _pendingRefreshEntities.Clear();
            PendingAuditEntries.Clear();
            PendingDataEvents.Clear();
            // Schema, _options, _logger, TimeTracker are config-derived and stable across
            // requests — keep them so pooled contexts don't require ConfigureServices() per use
        }

        public virtual Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            ResetState();
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();

            _logger?.LogDebug(GetType().Name + " is disposing...");
            ResetState();
            _options = null;
            Schema = null;
            TimeTracker = null;
            _logger = null;
        }

    }
}
