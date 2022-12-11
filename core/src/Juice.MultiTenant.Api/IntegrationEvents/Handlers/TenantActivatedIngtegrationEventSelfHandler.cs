using Finbuckle.MultiTenant.Stores;
using Juice.EventBus;
using Juice.MultiTenant.Api.IntegrationEvents.Events;
using Juice.MultiTenant.EF;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.Api.IntegrationEvents.Handlers
{
    /// <summary>
    /// Self handle TenantActivatedIngtegrationEvent to update distributed cache store if exists.
    /// </summary>
    public class TenantActivatedIngtegrationEventSelfHandler : IIntegrationEventHandler<TenantActivatedIntegrationEvent>
    {
        private readonly TenantStoreDbContext<Tenant> _context;
        private readonly DistributedCacheStore<Tenant>? _cacheStore;
        private readonly ILogger _logger;

        public TenantActivatedIngtegrationEventSelfHandler(
            TenantStoreDbContext<Tenant> dbContext,
            ILogger<TenantActivatedIngtegrationEventSelfHandler> logger,
            DistributedCacheStore<Tenant>? cacheStore = null)
        {
            _cacheStore = cacheStore;
            _context = dbContext;
            _logger = logger;
        }

        public async Task HandleAsync(TenantActivatedIntegrationEvent @event)
        {
            if (_cacheStore != null)
            {
                var tenant = await _context.TenantInfo.Where(t => t.Identifier == @event.TenantIdentifier).FirstOrDefaultAsync();
                if (tenant != null)
                {
                    var added = await _cacheStore.TryAddAsync(tenant);
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
