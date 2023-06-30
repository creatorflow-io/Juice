using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.EventBus;
using Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events;

namespace Juice.MultiTenant.Api.IntegrationEvents.Handlers
{
    /// <summary>
    /// Self handle TenantActivatedIngtegrationEvent to update distributed cache store if exists.
    /// </summary>
    public class TenantActivatedIngtegrationEventSelfHandler<TTenantInfo> : IIntegrationEventHandler<TenantActivatedIntegrationEvent>
        where TTenantInfo : class, ITenantInfo, new()
    {
        private readonly TenantStoreDbContext _context;
        private readonly DistributedCacheStore<TTenantInfo>? _cacheStore;
        private readonly ILogger _logger;

        public TenantActivatedIngtegrationEventSelfHandler(
            TenantStoreDbContext dbContext,
            ILogger<TenantActivatedIngtegrationEventSelfHandler<TTenantInfo>> logger,
            DistributedCacheStore<TTenantInfo>? cacheStore = null)
        {
            _cacheStore = cacheStore;
            _context = dbContext;
            _logger = logger;
        }

        public async Task HandleAsync(TenantActivatedIntegrationEvent @event)
        {
            if (_cacheStore != null)
            {
                var tenant = await _context.TenantInfo.Where(t => t.Identifier == @event.TenantIdentifier)
                    .Select(ti => new TenantInfo(ti.Id, ti.Identifier, ti.Name, ti.SerializedProperties, ti.ConnectionString, ti.OwnerUser))
                    .FirstOrDefaultAsync();
                if (tenant is TTenantInfo tenantInfo)
                {
                    var added = await _cacheStore.TryAddAsync(tenantInfo);
                    if (added)
                    {
                        _logger.LogInformation("Tenant {identifier} info was updated to DistributedCacheStore", @event.TenantIdentifier);
                    }
                    else
                    {
                        _logger.LogInformation("Tenant {identifier} info was not updated to DistributedCacheStore", @event.TenantIdentifier);
                    }
                }
                else
                {
                    _logger.LogWarning("Tenant {identifier} info was not found", @event.TenantIdentifier);
                }
            }
            else
            {
                _logger.LogInformation("DistributedCacheStore was not registerd in DI");
            }
        }
    }
}
