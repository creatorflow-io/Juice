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
        private readonly DbOptions? _options;
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
            this.SetAuditInformation(_logger);
            var changes = this.TrackingChanges(_logger);

            try
            {
                if (_options != null && _options.JsonPropertyBehavior == JsonPropertyBehavior.UpdateALL)
                {
                    return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cts.Token);
                }
                using (var transaction = Database.BeginTransaction())
                {
                    try
                    {
                        var (affects, refeshEntries) = await this.TryUpdateDynamicPropertyAsync(_logger);
                        if (this.HasUnsavedChanges())
                        {
                            affects = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cts.Token);
                        }

                        transaction.Commit();

                        await refeshEntries.RefreshEntriesAsync();
                        return affects;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
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
            this.SetAuditInformation(_logger);
            var changes = this.TrackingChanges(_logger);
            try
            {
                if (_options != null && _options.JsonPropertyBehavior == JsonPropertyBehavior.UpdateALL)
                {
                    return base.SaveChanges(acceptAllChangesOnSuccess);
                }
                using (var transaction = Database.BeginTransaction())
                {
                    try
                    {
                        var (affects, refeshEntries) = this.TryUpdateDynamicPropertyAsync(_logger).GetAwaiter().GetResult();
                        if (this.HasUnsavedChanges())
                        {
                            affects = base.SaveChanges(acceptAllChangesOnSuccess);
                        }

                        transaction.Commit();

                        refeshEntries.RefreshEntriesAsync().GetAwaiter().GetResult();
                        return affects;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
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
