using System.Security.Claims;
using Juice.EF.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EF
{
    public abstract partial class DbContextBase : DbContext, ISchemaDbContext, IAuditableDbContext
    {

        public string? Schema { get; protected set; }
        public string? User => _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;

        private IHttpContextAccessor? _httpContextAccessor;
        private ILogger? _logger;
        private IServiceProvider _serviceProvider;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _hasChanged;
        public bool HasChanged => _hasChanged;
        public DbContextBase(IServiceProvider serviceProvider, DbContextOptions options)
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
            try
            {
                AuditHandlers = serviceProvider.GetServices<IDataEventHandler>();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"[DbContextBase] failed to receive audit handlers. {ex.Message}");
            }

            var dbOptions = serviceProvider.GetService(typeof(DbOptions<>).MakeGenericType(GetType())) as DbOptions;
            Schema = dbOptions?.Schema;
        }

        protected abstract void ConfigureModel(ModelBuilder modelBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureModel(modelBuilder);

            var collections = _serviceProvider
                .GetServices<IModelConfiguration>()
                .Where(c => typeof(ModelConfigurationBase<>).MakeGenericType(GetType()).IsAssignableFrom(c.GetType()));
            foreach (var callback in collections)
            {
                callback.OnModelCreating(modelBuilder);
            }

        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            this.SetAuditInformation();
            var changes = this.TrackingChanges();

            try
            {
                using (var transaction = Database.BeginTransaction())
                {
                    var (affects, refeshEntries) = await this.TryUpdateDynamicPropertyAsync(_logger);
                    if (this.HasUnsavedChanges())
                    {
                        affects = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cts.Token);
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


        #region Audit
        public IEnumerable<IDataEventHandler>? AuditHandlers { get; set; }

        #endregion
    }
}
