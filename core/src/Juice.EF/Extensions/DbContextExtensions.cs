using Juice.Domain;
using Juice.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.EF.Extensions
{
    public static class DbContextExtensions
    {
        [Obsolete("This business has moved to the TrackingChanges method")]
        public static void SetAuditInformation<TContext>(this TContext context, ILogger? logger = default)
            where TContext : DbContext, IAuditableDbContext
        {
            return;
            try
            {
                var addedEntities = context.ChangeTracker.Entries()
                    .Where(entry => entry.State == EntityState.Added).ToList();
                var user = context.User;

                if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                {
                    logger.LogDebug("[Audit] Found {count} Added entries", addedEntities.Count);
                }

                addedEntities.ForEach(entry =>
                {
                    if (entry.Entity is ICreationInfo)
                    {
                        if (user != null && entry.Property(nameof(ICreationInfo.CreatedUser)).CurrentValue == null)
                        {
                            entry.Property(nameof(ICreationInfo.CreatedUser)).CurrentValue = user;
                        }
                        entry.Property(nameof(ICreationInfo.CreatedDate)).CurrentValue = DateTimeOffset.Now;
                    }
                    if (entry.Entity is IModificationInfo)
                    {
                        entry.Property(nameof(IModificationInfo.ModifiedUser)).CurrentValue = user;
                        entry.Property(nameof(IModificationInfo.ModifiedDate)).CurrentValue = DateTimeOffset.Now;
                    }

                    if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                    {
                        logger.LogDebug("[Audit] Setted audit info for entry {entryId}", entry.Property("Id").CurrentValue ?? "");
                    }
                });


                var editedEntities = context.ChangeTracker.Entries()
                    .Where(entry => entry.State == EntityState.Modified).ToList();
                if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                {
                    logger.LogDebug("[Audit] Found {count} Modified entries", editedEntities.Count);
                }
                editedEntities.ForEach(entry =>
                {
                    if (entry.Entity is IRemovable removeInfo && entry.Property(nameof(IRemovable.IsRemoved)).IsModified)
                    {
                        if (removeInfo.IsRemoved)
                        {
                            entry.Property(nameof(IRemovable.RemovedUser)).CurrentValue = user;
                            entry.Property(nameof(IRemovable.RemovedDate)).CurrentValue = DateTimeOffset.Now;
                        }
                        else
                        {
                            entry.Property(nameof(IRemovable.RestoredUser)).CurrentValue = user;
                            entry.Property(nameof(IRemovable.RestoredDate)).CurrentValue = DateTimeOffset.Now;
                        }
                    }
                    else if (entry.Entity is IModificationInfo)
                    {
                        entry.Property(nameof(IModificationInfo.ModifiedUser)).CurrentValue = user;
                        entry.Property(nameof(IModificationInfo.ModifiedDate)).CurrentValue = DateTimeOffset.Now;
                    }
                    if (entry.Entity is ICreationInfo)
                    {
                        entry.Property(nameof(ICreationInfo.CreatedDate)).IsModified = false;
                        entry.Property(nameof(ICreationInfo.CreatedUser)).IsModified = false;
                    }
                    if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                    {
                        logger.LogDebug("[Audit] Setted audit info for entry {entryId}", entry.Property("Id").CurrentValue ?? "");
                    }
                });

            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[Audit] Failed to set audit info {trace}", ex.StackTrace);
            }
        }

        public static void TrackingChanges<TContext>(this TContext context, ILogger? logger = default)
            where TContext : DbContext, IAuditableDbContext
        {
            try
            {
                context.ChangeTracker.DetectChanges();
                var user = context.User;

                var auditEntriesCount = 0;
                var dataEventsCount = 0;

                foreach (var entry in context.ChangeTracker.Entries())
                {
                    if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    {
                        continue;
                    }

                    #region basic audit info

                    if (entry.State == EntityState.Added)
                    {
                        #region set creation info
                        if (entry.Entity is ICreationInfo)
                        {
                            if (user != null && entry.Property(nameof(ICreationInfo.CreatedUser)).CurrentValue == null)
                            {
                                entry.Property(nameof(ICreationInfo.CreatedUser)).CurrentValue = user;
                            }
                            entry.Property(nameof(ICreationInfo.CreatedDate)).CurrentValue = DateTimeOffset.Now;
                        }
                        #endregion

                        #region add data event
                        if (entry.Metadata.IsNoticeFor(EntityStates.Created)) {
                            var eventType = context.DataEventType(nameof(DataEvents.Inserted));
                            if(eventType!= null)
                            {
                                context.PendingDataEvents.Add(DataEvents.Inserted.CreateDataEvent(eventType, entry.Entity));
                                dataEventsCount++;
                            }
                        }
                        #endregion
                    }
                    else if(entry.State == EntityState.Modified)
                    {
                        #region set modification info
                        if (entry.Entity is IRemovable removeInfo && entry.Property(nameof(IRemovable.IsRemoved)).IsModified)
                        {
                            if (removeInfo.IsRemoved)
                            {
                                entry.Property(nameof(IRemovable.RemovedUser)).CurrentValue = user;
                                entry.Property(nameof(IRemovable.RemovedDate)).CurrentValue = DateTimeOffset.Now;
                            }
                            else
                            {
                                entry.Property(nameof(IRemovable.RestoredUser)).CurrentValue = user;
                                entry.Property(nameof(IRemovable.RestoredDate)).CurrentValue = DateTimeOffset.Now;
                            }
                        }
                        else if (entry.Entity is IModificationInfo)
                        {
                            entry.Property(nameof(IModificationInfo.ModifiedUser)).CurrentValue = user;
                            entry.Property(nameof(IModificationInfo.ModifiedDate)).CurrentValue = DateTimeOffset.Now;
                        }
                        if (entry.Entity is ICreationInfo)
                        {
                            entry.Property(nameof(ICreationInfo.CreatedDate)).IsModified = false;
                            entry.Property(nameof(ICreationInfo.CreatedUser)).IsModified = false;
                        }
                        #endregion

                        #region add data event
                        if (entry.Metadata.IsNoticeFor(EntityStates.Modified))
                        {
                            var eventType = context.DataEventType(nameof(DataEvents.Modified));
                            if (eventType != null)
                            {
                                context.PendingDataEvents.Add(DataEvents.Modified.CreateDataEvent(eventType, entry.Entity));
                                dataEventsCount++;
                            }
                        }
                        #endregion
                    }
                    else if(entry.State == EntityState.Deleted)
                    {
                        #region add data event
                        if (entry.Metadata.IsNoticeFor(EntityStates.Deleted))
                        {
                            var eventType = context.DataEventType(nameof(DataEvents.Deleted));
                            if (eventType != null)
                            {
                                context.PendingDataEvents.Add(DataEvents.Modified.CreateDataEvent(eventType, entry.Entity));
                                dataEventsCount++;
                            }
                        }
                        #endregion
                    }
                    #endregion

                    #region advanced audit info
                    if (!entry.Metadata.IsAuditable())
                    {
                        continue;
                    }

                    var auditEntry = new AuditEntry(entry, entry.Metadata?.GetTableName(),
                        entry.State == EntityState.Added ? DataEvents.Inserted
                        : entry.State == EntityState.Deleted ? DataEvents.Deleted
                        : entry.State == EntityState.Modified ? DataEvents.Modified
                        : null)
                    {
                        Database = context.Database.GetDbConnection().Database,
                        Schema = entry.Metadata?.GetSchema(),
                        User = user
                    };
                    if (!auditEntry.HasDataEvent) { continue; }

                    var tableIdentifier = entry.Metadata != null ? StoreObjectIdentifier.Create(entry.Metadata, StoreObjectType.Table)
                        : default;
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
                                        || property.Metadata.Name == nameof(ICreationInfo.CreatedDate)
                                        || property.Metadata.Name == nameof(ICreationInfo.CreatedUser)
                                        || property.Metadata.Name == nameof(IModificationInfo.ModifiedDate)
                                        || property.Metadata.Name == nameof(IModificationInfo.ModifiedUser)
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
                                            // removed or modified property
                                            if (!expandable1.CurrentPropertyValues.ContainsKey(kvp.Key) ||
                                                expandable1.CurrentPropertyValues[kvp.Key]?.ToString() != kvp.Value?.ToString())
                                            {
                                                auditEntry.OriginalValues[kvp.Key] = kvp.Value;
                                                auditEntry.CurrentValues[kvp.Key] =
                                                    expandable1.CurrentPropertyValues.ContainsKey(kvp.Key) ?
                                                    expandable1.CurrentPropertyValues[kvp.Key] : null;
                                            }
                                        }
                                        foreach (var kvp in expandable1.CurrentPropertyValues)
                                        {
                                            // added property
                                            if (!expandable1.OriginalPropertyValues.ContainsKey(kvp.Key))
                                            {
                                                auditEntry.OriginalValues[kvp.Key] = null;
                                                auditEntry.CurrentValues[kvp.Key] = kvp.Value;
                                            }
                                        }
                                        if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                                        {
                                            logger?.LogDebug("[TrackingChanges] found {count} changed dynamic properties", auditEntry.CurrentValues.Count);
                                        }
                                        break;
                                    }
                                    auditEntry.OriginalValues[propertyName] = property.OriginalValue;
                                    auditEntry.CurrentValues[propertyName] = property.CurrentValue;
                                }
                                break;
                        }
                    }

                    context.PendingAuditEntries.Add(auditEntry);
                    auditEntriesCount++;
                    #endregion
                }

                if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                {
                    logger?.LogDebug("[TrackingChanges] collected {count} audit entries, {count1} data events", auditEntriesCount, dataEventsCount);
                }

            }
            catch (Exception ex)
            {
                logger?.LogError(ex, $"[TrackingChanges] {ex.Message}");
            }
        }

        public static bool HasUnsavedChanges<TContext>(this TContext context)
            where TContext : DbContext
        {
            return context.ChangeTracker.Entries().Any(e => e.State == EntityState.Added
                                                     || e.State == EntityState.Modified
                                                     || e.State == EntityState.Deleted);
        }

        public static async Task<(int Affected, HashSet<EntityEntry> RefreshEntries)> TryUpdateDynamicPropertyAsync<TContext>(this TContext context,
            ILogger? logger = default)
            where TContext : DbContext
        {
            var (refresh, updates, args) = await context.GetExpandablePropertiesUpdateSqlAsync(logger);
            if (updates.Any())
            {
                var sql = string.Join(";", updates);
                var affects = await context.Database.ExecuteSqlRawAsync(sql, args);

                return (affects, refresh);
            }
            else
            {
                return (0, refresh);
            }
        }

        public static async Task<(HashSet<EntityEntry> refresh, IEnumerable<string> SQL, object[] ARGs)> GetExpandablePropertiesUpdateSqlAsync<TContext>(this TContext context, ILogger? logger = default)
            where TContext : DbContext
        {
            await Task.Yield();
            var _refreshEntries = new HashSet<EntityEntry>();

            var provider = context.Database.ProviderName;

            var editedEntities = context.ChangeTracker.Entries<IExpandable>().Where(entry => entry.State == EntityState.Modified).ToList();
            var updates = new List<string>();
            var args = new List<object>();
            foreach (var entry in editedEntities)
            {
                var tableIdentifier = StoreObjectIdentifier.Create(entry.Metadata, StoreObjectType.Table);
                if (tableIdentifier.HasValue)
                {
                    // Finding property that map to Properties column
                    var propertyColumn = "Properties";
                    var property = entry.Properties.Where(p => p.Metadata.GetColumnName(tableIdentifier.Value) == propertyColumn).FirstOrDefault();
                    if (property != null)
                    {
                        property.IsModified = false;
                        _refreshEntries.Add(entry);
                        var expandable = entry.Entity;

                        // Get Key value
                        var primaryKey = entry.Metadata.FindPrimaryKey();
                        var keyProp = primaryKey?.Properties?.Select(p => p.PropertyInfo)?.Single();
                        var keyColumn = primaryKey?.Properties?.Select(p => p.GetColumnName(tableIdentifier.Value))?.Single();
                        var key = keyProp?.GetValue(expandable, null);
                        if (key != null && !string.IsNullOrEmpty(keyColumn))
                        {
                            foreach (var kvp in expandable.OriginalPropertyValues)
                            {
                                var currentValue = expandable.CurrentPropertyValues[kvp.Key];
                                if (currentValue != kvp.Value)
                                {
                                    var value = JsonConvert.SerializeObject(currentValue);

                                    var (sql, sqlargs) = provider == "Npgsql.EntityFrameworkCore.PostgreSQL" ?
                                         PostgreSQLJsonModify(entry.Metadata, args.Count, kvp.Key, value, keyColumn, key, propertyColumn)
                                        : SqlServerJsonModify(entry.Metadata, args.Count, kvp.Key, value, keyColumn, key, propertyColumn);

                                    updates.Add(sql);
                                    args.AddRange(sqlargs);
                                    if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                                    {
                                        logger?.LogDebug(sql);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return (_refreshEntries, updates, args.ToArray());
        }

        private static (string SQL, object[] ARGs) SqlServerJsonModify(IEntityType metadata,
            int argCount, string propertyKey, string propertyValue,
            string keyColumn, object keyValue, string propertyColumn)
        {
            var token = JToken.Parse(propertyValue);
            if (token is JArray || token is JObject)
            {
                propertyValue = token.ToString(Formatting.None);
                var sql = $"Update [{metadata.GetSchema()}].[{metadata.GetTableName()}] set [{propertyColumn}]=JSON_MODIFY([{propertyColumn}], '$.\"{propertyKey}\"', JSON_QUERY  ({{{argCount}}})) where [{keyColumn}] = {{{argCount + 1}}}";

                return (sql, new object[] { propertyValue, keyValue });
            }
            else
            {
                propertyValue = propertyValue.Trim('"');
                var sql = $"Update [{metadata.GetSchema()}].[{metadata.GetTableName()}] set [{propertyColumn}]=JSON_MODIFY([{propertyColumn}], '$.\"{propertyKey}\"', {{{argCount}}}) where [{keyColumn}] = {{{argCount + 1}}}";

                return (sql, new object[] { propertyValue, keyValue });
            }
        }

        private static (string SQL, object[] ARGs) PostgreSQLJsonModify(IEntityType metadata,
            int argCount, string propertyKey, string propertyValue,
            string keyColumn, object keyValue, string propertyColumn)
        {
            var token = JToken.Parse(propertyValue);
            propertyValue = token.ToString(Formatting.None);
            if (token is JObject)
            {
                var sql = $"Update \"{metadata.GetSchema()}\".\"{metadata.GetTableName()}\" set \"{propertyColumn}\"=jsonb_set(\"{propertyColumn}\", '{{{{{propertyKey}}}}}', jsonb '{{{propertyValue}}}', true) where \"{keyColumn}\" = {{{argCount}}}";

                return (sql, new object[] { keyValue });
            }
            else
            {
                var sql = $"Update \"{metadata.GetSchema()}\".\"{metadata.GetTableName()}\" set \"{propertyColumn}\"=jsonb_set(\"{propertyColumn}\", '{{{{{propertyKey}}}}}', jsonb '{propertyValue}', true) where \"{keyColumn}\" = {{{argCount}}}";

                return (sql, new object[] { keyValue });
            }

        }

        #region Expandable entity based on JSON column

        public static async Task RefreshEntriesAsync(this HashSet<EntityEntry> refreshEntries)
        {
            if (refreshEntries.Any())
            {
                foreach (var entry in refreshEntries)
                {
                    await entry.ReloadAsync();
                }
            }
        }

        #endregion

        public static async Task MigrateAsync<TContext>(this TContext context)
             where TContext : DbContext
        {
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[{typeof(TContext).Name}] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine($"[{typeof(TContext).Name}] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }
        }

        #region UnitOfWork

        public static IDbContextTransaction? GetCurrentTransaction(this DbContext context)
            => context.Database.CurrentTransaction;
        #endregion
    }
}
