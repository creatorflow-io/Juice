using System.Security.Claims;
using Juice.Domain.Events;
using Juice.EF.Extensions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EF
{

    public abstract partial class DbContextBase: UnitOfWork,
        ISchemaDbContext, IAuditableDbContext
    {
        #region Schema context
        public string? Schema { get; protected set; }
        #endregion

        #region Auditable context

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
        public string? User { get; protected set; }
        public List<DataEvent> PendingDataEvents { get; set; } = new List<DataEvent>();
        public List<AuditEntry> PendingAuditEntries { get; set; } = new List<AuditEntry>();

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
            if (!this.HasActiveTransaction)
            {
                _mediator.DispatchDataChangeEventsAsync(this, _logger).GetAwaiter().GetResult();
            }
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.TrackingChanges(_logger);

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
            this.TrackingChanges(_logger);
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

        #region UnitOfWork
        protected override async Task OnTransactionCommittedAsync()
        {
            if (_pendingRefreshEntities != null)
            {
                await _pendingRefreshEntities.RefreshEntriesAsync();
            }
            await _mediator.DispatchDataChangeEventsAsync(this, _logger);
        }
        #endregion

        public override void Dispose()
        {
            base.Dispose();

            // cleanup self services and data
            _logger?.LogDebug(GetType().Name + " is disposing...");
            _options = null;
            Schema = null;
            User = null;
            _mediator = null;
            _logger = null;
            _pendingRefreshEntities.Clear();
            PendingAuditEntries = null;
        }

    }
}
