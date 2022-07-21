using System.Security.Claims;
using Juice.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.EF
{
    public abstract partial class DbContextBase : DbContext
    {
        private IHttpContextAccessor _httpContextAccessor;
        private ILogger _logger;
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
        }

        protected abstract void ConfigureModel(ModelBuilder modelBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
            SetAuditInformation();
            var changes = TrackingChanges();
            try
            {
                var (updates, args) = await GetExpandablePropertiesUpdateSqlAsync().ConfigureAwait(false);
                if (updates.Any())
                {
                    using (var transaction = Database.BeginTransaction())
                    {
                        var sql = string.Join(";", updates);

                        var affects = await Database.ExecuteSqlRawAsync(sql, args);
                        if (HasUnsavedChanges())
                        {
                            affects = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cts.Token);
                        }
                        RefreshEntriesAsync().GetAwaiter().GetResult();
                        transaction.Commit();
                        return affects;
                    }
                }
                else
                {
                    return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cts.Token);
                }
            }
            finally
            {
                NotificationChanges(changes);
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {

            SetAuditInformation();
            var changes = TrackingChanges();
            try
            {
                var (updates, args) = GetExpandablePropertiesUpdateSqlAsync().GetAwaiter().GetResult();
                if (updates.Any())
                {
                    using (var transaction = Database.BeginTransaction())
                    {
                        var sql = string.Join(";", updates);

                        var affects = Database.ExecuteSqlRaw(sql, args);
                        if (HasUnsavedChanges())
                        {
                            affects = base.SaveChanges(acceptAllChangesOnSuccess);
                        }
                        RefreshEntriesAsync().GetAwaiter().GetResult();
                        transaction.Commit();
                        return affects;
                    }
                }
                else
                {
                    return base.SaveChanges(acceptAllChangesOnSuccess);
                }
            }
            finally
            {
                NotificationChanges(changes);
            }
        }

        public bool HasUnsavedChanges()
        {
            return ChangeTracker.Entries().Any(e => e.State == EntityState.Added
                                                      || e.State == EntityState.Modified
                                                      || e.State == EntityState.Deleted);
        }

        #region Expandable entity based on JSON column
        private async Task<(IEnumerable<string> SQL, object[] ARGs)> GetExpandablePropertiesUpdateSqlAsync()
        {
            await Task.Yield();
            var editedEntities = ChangeTracker.Entries<IExpandable>().Where(entry => entry.State == EntityState.Modified).ToList();
            var updates = new List<string>();
            var args = new List<object>();
            foreach (var entry in editedEntities)
            {
                var tableIdentifier = StoreObjectIdentifier.Create(entry.Metadata, StoreObjectType.Table);
                if (tableIdentifier.HasValue)
                {
                    // Finding property that map to Properties column
                    var colName = "Properties";
                    var property = entry.Properties.Where(p => p.Metadata.GetColumnName(tableIdentifier.Value) == colName).FirstOrDefault();
                    if (property != null)
                    {
                        property.IsModified = false;
                        _refreshEntries.Add(entry);
                        var expandable = entry.Entity;

                        // Get Key value
                        var primaryKey = entry.Metadata.FindPrimaryKey();
                        var keyProp = primaryKey.Properties.Select(p => p.PropertyInfo).Single();
                        var keyColumn = primaryKey.Properties.Select(p => p.GetColumnName(tableIdentifier.Value)).Single();
                        var key = keyProp.GetValue(expandable, null);
                        if (key != null && !string.IsNullOrEmpty(keyColumn))
                        {
                            foreach (var kvp in expandable.OriginalPropertyValues)
                            {
                                var currentValue = expandable.CurrentPropertyValues[kvp.Key];
                                if (currentValue != kvp.Value)
                                {
                                    var value = JsonConvert.SerializeObject(currentValue);
                                    var sql = "";

                                    var token = JToken.Parse(value);

                                    if (token is JArray || token is JObject)
                                    {
                                        value = token.ToString(Formatting.None);
                                        sql = $"Update {entry.Metadata.GetSchema()}.[{entry.Metadata.GetTableName()}] set {colName}=JSON_MODIFY({colName}, '$.\"{kvp.Key}\"', JSON_QUERY  ({{{args.Count}}})) where {keyColumn} = {{{args.Count + 1}}}";
                                    }
                                    else
                                    {
                                        value = value.Trim('"');
                                        sql = $"Update {entry.Metadata.GetSchema()}.[{entry.Metadata.GetTableName()}] set {colName}=JSON_MODIFY({colName}, '$.\"{kvp.Key}\"', {{{args.Count}}}) where {keyColumn} = {{{args.Count + 1}}}";
                                    }

                                    updates.Add(sql);
                                    args.Add(value);
                                    args.Add(key);
                                    _logger?.LogDebug(sql);
                                }
                            }
                        }
                    }
                }
            }
            return (updates, args.ToArray());
        }

        private HashSet<EntityEntry> _refreshEntries = new HashSet<EntityEntry>();

        private async Task RefreshEntriesAsync()
        {
            if (_refreshEntries.Any())
            {
                foreach (var entry in _refreshEntries)
                {
                    await entry.ReloadAsync();
                }
            }
        }

        #endregion

        #region Audit
        public IEnumerable<IDataEventHandler> AuditHandlers { get; set; }

        private void SetAuditInformation()
        {
            try
            {
                var addedEntities = ChangeTracker.Entries<IAuditable>().Where(entry => entry.State == EntityState.Added).ToList();
                var user = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;

                addedEntities.ForEach(entry =>
                {
                    entry.Entity.SetOnceCreatedUser(user);

                    entry.Entity.UpdateModifiedUser(user);
                });

                var editedEntities = ChangeTracker.Entries<IAuditable>().Where(entry => entry.State == EntityState.Modified).ToList();

                editedEntities.ForEach(entry =>
                {
                    entry.Property(nameof(IAuditable.CreatedDate)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedUser)).IsModified = false;
                    entry.Property(nameof(IAuditable.ModifiedDate)).CurrentValue = DateTimeOffset.Now;

                    entry.Entity.UpdateModifiedUser(user);

                });

            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[DbContextBase][SetAuditInformation][Failed]");
            }

        }

        private IEnumerable<AuditEntry> TrackingChanges()
        {
            try
            {
                if (AuditHandlers?.Any() ?? false)
                {
                    ChangeTracker.DetectChanges();
                    var user = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
                    var auditEntries = new List<AuditEntry>();
                    foreach (var entry in ChangeTracker.Entries())
                    {
                        if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                        {
                            continue;
                        }

                        var auditEntry = new AuditEntry(entry)
                        {
                            Table = entry.Metadata.GetTableName(),
                            Database = Database.GetDbConnection().Database,
                            Schema = entry.Metadata.GetSchema(),
                            User = user,
                            DataEvent = entry.State == EntityState.Added ? DataEvents.Inserted
                            : entry.State == EntityState.Deleted ? DataEvents.Deleted
                            : entry.State == EntityState.Modified ? DataEvents.Modified
                            : null
                        };
                        if (auditEntry.DataEvent == null) { continue; }

                        auditEntries.Add(auditEntry);

                        var tableIdentifier = StoreObjectIdentifier.Create(entry.Metadata, StoreObjectType.Table);
                        foreach (var property in entry.Properties)
                        {
                            if (property.IsTemporary)
                            {
                                // value will be generated by the database, get the value after saving
                                auditEntry.TemporaryProperties.Add(property);
                                continue;
                            }

                            var propertyName = property.Metadata.Name;
                            if (property.Metadata.IsPrimaryKey())
                            {
                                auditEntry.KeyValues[propertyName] = property.CurrentValue;
                                continue;
                            }

                            switch (entry.State)
                            {
                                case EntityState.Added:
                                    if (entry.Entity is IExpandable expandable
                                            && tableIdentifier.HasValue
                                            && property.Metadata.GetColumnName(tableIdentifier.Value) == "Properties")
                                    {
                                        foreach (var kvp in expandable.CurrentPropertyValues)
                                        {
                                            auditEntry.CurrentValues[kvp.Key] = kvp.Value;
                                        }

                                        break;
                                    }
                                    auditEntry.CurrentValues[propertyName] = property.CurrentValue;
                                    break;

                                case EntityState.Deleted:
                                    auditEntry.OriginalValues[propertyName] = property.OriginalValue;
                                    break;

                                case EntityState.Modified:
                                    if (property.IsModified)
                                    {
                                        if ((property.CurrentValue == null && property.OriginalValue == null)
                                            || (property.CurrentValue != null && property.OriginalValue != null
                                            && property.CurrentValue.Equals(property.OriginalValue))
                                            || property.Metadata.Name == nameof(IAuditable.CreatedDate)
                                            || property.Metadata.Name == nameof(IAuditable.CreatedUser)
                                            || property.Metadata.Name == nameof(IAuditable.ModifiedDate)
                                            || property.Metadata.Name == nameof(IAuditable.ModifiedUser)
                                            )
                                        {
                                            break;
                                        }
                                        // handle for expanable entity based on JSON column
                                        if (entry.Entity is IExpandable expandable1
                                            && tableIdentifier.HasValue
                                            && property.Metadata.GetColumnName(tableIdentifier.Value) == "Properties")
                                        {
                                            foreach (var kvp in expandable1.OriginalPropertyValues)
                                            {
                                                if (expandable1.CurrentPropertyValues.ContainsKey(kvp.Key) && expandable1.CurrentPropertyValues[kvp.Key] != kvp.Value)
                                                {
                                                    auditEntry.OriginalValues[kvp.Key] = kvp.Value;
                                                    auditEntry.CurrentValues[kvp.Key] = expandable1.CurrentPropertyValues[kvp.Key];
                                                }
                                            }

                                            break;
                                        }
                                        auditEntry.OriginalValues[propertyName] = property.OriginalValue;
                                        auditEntry.CurrentValues[propertyName] = property.CurrentValue;
                                    }
                                    break;
                            }
                        }
                    }
                    return auditEntries;
                }

            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[DbContextBase][TrackingChanges][Failed]");
            }
            return null;
        }

        private void NotificationChanges(IEnumerable<AuditEntry> auditEntries)
        {
            _hasChanged = true;

            try
            {
                if ((AuditHandlers?.Any() ?? false) && auditEntries != null && auditEntries.Any())
                {
                    foreach (var auditEntry in auditEntries)
                    {
                        // Get the final value of the temporary properties
                        foreach (var prop in auditEntry.TemporaryProperties)
                        {
                            if (prop.Metadata.IsPrimaryKey())
                            {
                                auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                            }
                            else
                            {
                                auditEntry.CurrentValues[prop.Metadata.Name] = prop.CurrentValue;
                            }
                        }

                        // Save the Audit entry
                        foreach (var handler in AuditHandlers)
                        {
                            try
                            {
                                handler.HandleAsync(auditEntry.DataEvent.Create(auditEntry.ToAudit())).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, $"[DbContextBase] failed to handle audit event on {handler.GetType().FullName}. {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[DbContextBase][NotificationChanges] {0}", ex.Message);
            }
        }

        private class AuditEntry
        {
            public AuditEntry(EntityEntry entry)
            {
                Entry = entry;
            }
            public EntityEntry Entry { get; }
            public DataEvent DataEvent { get; set; }
            public string User { get; set; }
            public string Database { get; set; }
            public string Schema { get; set; }
            public string Table { get; set; }
            public Dictionary<string, object> KeyValues { get; } = new Dictionary<string, object>();
            public Dictionary<string, object> OriginalValues { get; } = new Dictionary<string, object>();
            public Dictionary<string, object> CurrentValues { get; } = new Dictionary<string, object>();
            public List<PropertyEntry> TemporaryProperties { get; } = new List<PropertyEntry>();

            public bool HasTemporaryProperties => TemporaryProperties.Any();

            public AuditRecord ToAudit()
            {
                var audit = new AuditRecord
                {
                    Table = Table,
                    Database = Database,
                    Schema = Schema,
                    User = User,
                    KeyValues = KeyValues,
                    CurrentValues = CurrentValues,
                    OriginalValues = OriginalValues,
                    Entity = Entry.Entity
                };

                return audit;
            }
        }

        #endregion
    }
}
