using Juice.Integrations.EventBus;
using Juice.MultiTenant.Api.IntegrationEvents.Events;
using Juice.MultiTenant.EF;
using Juice.MultiTenant.Events;

namespace Juice.MultiTenant.Api.DomainEventHandlers
{
    public class TenantDeactivatedDomainEventHandler : INotificationHandler<TenantDeactivatedDomainEvent>
    {
        private IIntegrationEventService<TenantStoreDbContext<Tenant>> _integrationService;
        private readonly ILoggerFactory _logger;
        public TenantDeactivatedDomainEventHandler(ILoggerFactory logger, IIntegrationEventService<TenantStoreDbContext<Tenant>> integrationService)
        {
            _logger = logger;
            _integrationService = integrationService;
        }
        public async Task Handle(TenantDeactivatedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TenantDeactivatedDomainEventHandler>()
            .LogTrace("Tenant with Identifier: {Identifier} has been successfully activated",
                notification.Tenant.Identifier);

            var tenantDeactivatedIntegrationEvent = new TenantDeactivatedIntegrationEvent(notification.Tenant.Identifier);
            await _integrationService.AddAndSaveEventAsync(tenantDeactivatedIntegrationEvent);
        }
    }
}
