using Finbuckle.MultiTenant.Stores;
using Juice.EventBus;
using Juice.MultiTenant.Api.IntegrationEvents.Events;

namespace Juice.MultiTenant.Api.IntegrationEvents.Handlers
{
    /// <summary>
    /// Self handle TenantDeactivatedIntegrationEvent to update distributed cache store if exists.
    /// </summary>
    public class TenantDeactivatedIngtegrationEventSelfHandler : IIntegrationEventHandler<TenantDeactivatedIntegrationEvent>
    {
        private readonly DistributedCacheStore<Tenant>? _cacheStore;
        private readonly ILogger _logger;

        public TenantDeactivatedIngtegrationEventSelfHandler(
            ILogger<TenantDeactivatedIngtegrationEventSelfHandler> logger,
            DistributedCacheStore<Tenant>? cacheStore = null)
        {
            _cacheStore = cacheStore;
            _logger = logger;
        }

        public async Task HandleAsync(TenantDeactivatedIntegrationEvent @event)
        {
            if (_cacheStore != null)
            {
                var removed = await _cacheStore.TryRemoveAsync(@event.TenantIdentifier);
                if (removed)
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
                _logger.LogInformation("DistributedCacheStore was not registerd in DI");
            }
        }
    }
}
