using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.MultiTenant.AspNetCore
{
    /// <summary>A tenant as the pattern sees it: its Id, its Identifier and name for display, and whether it may be served.</summary>
    public sealed record TenantRecord(Guid Id, string? Identifier, string? Name, TenantAvailability Availability)
    {
        /// <summary>The Id in its standard form, as stored and passed.</summary>
        public string IdText => TenantIds.Format(Id);
    }

    /// <summary>Where tenants are looked up: juice-tenants (through a Finbuckle store), or a fixed list (tests, local stacks).</summary>
    public interface ITenantDirectory
    {
        ValueTask<TenantRecord?> FindAsync(Guid id, CancellationToken ct = default);

        /// <summary>For browser apps: the tenant by the Identifier in their URL.</summary>
        ValueTask<TenantRecord?> FindByIdentifierAsync(string identifier, CancellationToken ct = default);
    }

    public static class TenantDirectoryExtensions
    {
        /// <summary>
        /// Whether background work for the tenant may run now: <see cref="TenantAvailability.Active"/> runs. Anything else (an unknown tenant
        /// is <see cref="TenantAvailability.Inactive"/>) means the caller holds the work, not drops it, until the tenant is active again.
        /// </summary>
        public static async ValueTask<TenantAvailability> AvailabilityAsync(this ITenantDirectory directory, Guid id, CancellationToken ct = default)
            => (await directory.FindAsync(id, ct))?.Availability ?? TenantAvailability.Inactive;
    }

    /// <summary>
    /// Forgets what the directory remembers about a tenant, when a lifecycle event says it changed. Always registered; a directory that
    /// remembers nothing ignores it.
    /// </summary>
    public interface ITenantDirectoryCache
    {
        void Invalidate(Guid id);
    }

    internal sealed class NoTenantDirectoryCache : ITenantDirectoryCache
    {
        public void Invalidate(Guid id)
        {
        }
    }

    /// <summary>Sets Finbuckle's context to a tenant the directory found, for the request.</summary>
    internal interface ITenantContextBridge
    {
        /// <summary>The tenant, and how to enter it in Finbuckle for the request (with the very tenant found, not a second read).</summary>
        ValueTask<(TenantRecord? Tenant, Func<IDisposable?>? Enter)> FindForRequestAsync(Guid id);
    }

    /// <summary>A fixed list of tenants: tests, local stacks, and deployments without juice-tenants.</summary>
    public sealed class FixedTenantDirectory(IEnumerable<TenantRecord> tenants) : ITenantDirectory
    {
        private readonly Dictionary<Guid, TenantRecord> _byId = tenants.ToDictionary(t => t.Id);

        public ValueTask<TenantRecord?> FindAsync(Guid id, CancellationToken ct = default)
            => ValueTask.FromResult(_byId.GetValueOrDefault(id));

        public ValueTask<TenantRecord?> FindByIdentifierAsync(string identifier, CancellationToken ct = default)
            => ValueTask.FromResult(_byId.Values.FirstOrDefault(t => string.Equals(t.Identifier, identifier, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// The tenants of a Finbuckle store (juice-tenants' gRPC store, or its EF store), by Id, remembered for
    /// <see cref="TenancyOptions.CacheDuration"/> (and an unknown one for <see cref="TenancyOptions.UnknownCacheDuration"/>) in a cache of its
    /// own. The store is asked by the Id's standard form, then by the old base32 form for a store not yet rewritten. The request's Finbuckle
    /// context is set from the tenant found, without asking the store again.
    /// </summary>
    public sealed class StoreTenantDirectory<TTenant>(IServiceScopeFactory scopes, IServiceProvider services, IOptions<TenancyOptions> options)
        : ITenantDirectory, ITenantDirectoryCache, ITenantContextBridge, IDisposable
        where TTenant : class, ITenantInfo, new()
    {
        private sealed record Entry(TenantRecord? Record, TTenant? Tenant);

        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        public async ValueTask<TenantRecord?> FindAsync(Guid id, CancellationToken ct = default)
            => (await FindEntryAsync(id)).Record;

        public async ValueTask<TenantRecord?> FindByIdentifierAsync(string identifier, CancellationToken ct = default)
        {
            using var scope = scopes.CreateScope();
            foreach (var store in scope.ServiceProvider.GetServices<IMultiTenantStore<TTenant>>())
            {
                var tenant = await store.TryGetByIdentifierAsync(identifier);
                if (tenant is not null && TenantIds.TryParse(tenant.Id, out var id))
                {
                    var entry = new Entry(Record(tenant, id), tenant);
                    _cache.Set(id, entry, options.Value.CacheDuration);
                    return entry.Record;
                }
            }
            return null;
        }

        public void Invalidate(Guid id) => _cache.Remove(id);

        public async ValueTask<(TenantRecord? Tenant, Func<IDisposable?>? Enter)> FindForRequestAsync(Guid id)
        {
            var entry = await FindEntryAsync(id);
            var resolver = services.GetService<IScopedTenantResolver<TTenant>>();
            return (entry.Record, entry.Tenant is { } found && resolver is not null ? () => resolver.Resolve(found) : null);
        }

        public void Dispose() => _cache.Dispose();

        private async ValueTask<Entry> FindEntryAsync(Guid id)
        {
            if (_cache.TryGetValue(id, out Entry? cached) && cached is not null)
            {
                return cached;
            }
            using var scope = scopes.CreateScope();
            var entry = new Entry(null, null);
            foreach (var store in scope.ServiceProvider.GetServices<IMultiTenantStore<TTenant>>())
            {
                var tenant = await store.TryGetAsync(TenantIds.Format(id)) ?? await store.TryGetAsync(TenantIds.FormatBase32(id));
                if (tenant is not null)
                {
                    entry = new Entry(Record(tenant, id), tenant);
                    break;
                }
            }
            _cache.Set(id, entry, entry.Record is null ? options.Value.UnknownCacheDuration : options.Value.CacheDuration);
            return entry;
        }

        private static TenantRecord Record(TTenant tenant, Guid id)
            => new(id, tenant.Identifier, tenant.Name, TenantStatuses.Availability(tenant));
    }
}
