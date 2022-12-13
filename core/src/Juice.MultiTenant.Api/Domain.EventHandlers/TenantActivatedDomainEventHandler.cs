using Juice.Integrations.EventBus;
using Juice.MultiTenant.Api.IntegrationEvents.Events;
using Juice.MultiTenant.Domain.Events;
using Juice.MultiTenant.EF;

namespace Juice.MultiTenant.Api.Domain.EventHandlers
{
    internal class TenantActivatedDomainEventHandler : INotificationHandler<TenantActivatedDomainEvent>
    {
        private IIntegrationEventService<TenantStoreDbContext<Tenant>> _integrationService;
        private readonly ILoggerFactory _logger;
        public TenantActivatedDomainEventHandler(ILoggerFactory logger, IIntegrationEventService<TenantStoreDbContext<Tenant>> integrationService)
        {
            _logger = logger;
            _integrationService = integrationService;
        }
        public async Task Handle(TenantActivatedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TenantActivatedDomainEventHandler>()
            .LogTrace("Tenant with Identifier: {Identifier} has been successfully activated",
                notification.Tenant.Identifier);

            var tenantActivatedIntegrationEvent = new TenantActivatedIntegrationEvent(notification.Tenant.Identifier);
            await _integrationService.AddAndSaveEventAsync(tenantActivatedIntegrationEvent);
        }
    }
}
