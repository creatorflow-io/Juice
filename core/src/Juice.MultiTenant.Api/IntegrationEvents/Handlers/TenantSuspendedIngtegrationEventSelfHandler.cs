using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.EventBus;
using Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events;

namespace Juice.MultiTenant.Api.IntegrationEvents.Handlers
{
    /// <summary>
    /// Self handle TenantDeactivatedIntegrationEvent to update distributed cache store if exists.
    /// </summary>
    public class TenantSuspendedIngtegrationEventSelfHandler<TTenantInfo> : IIntegrationEventHandler<TenantSuspendedIntegrationEvent>
        where TTenantInfo : class, ITenantInfo, new()
    {
        private readonly DistributedCacheStore<TTenantInfo>? _cacheStore;
        private readonly ILogger _logger;

        public TenantSuspendedIngtegrationEventSelfHandler(
            ILogger<TenantSuspendedIngtegrationEventSelfHandler<TTenantInfo>> logger,
            DistributedCacheStore<TTenantInfo>? cacheStore = null)
        {
            _cacheStore = cacheStore;
            _logger = logger;
        }

        public async Task HandleAsync(TenantSuspendedIntegrationEvent @event)
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
