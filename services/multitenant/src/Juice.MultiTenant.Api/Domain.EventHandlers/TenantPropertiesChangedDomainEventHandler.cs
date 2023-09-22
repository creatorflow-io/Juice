using Juice.Integrations.EventBus;
using Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events;
using Juice.MultiTenant.Domain.Events;
using Juice.MultiTenant.EF;

namespace Juice.MultiTenant.Api.Domain.EventHandlers
{
    internal class TenantPropertiesChangedDomainEventHandler :
        INotificationHandler<TenantPropertiesChangedDomainEvent>
    {
        private IIntegrationEventService<TenantStoreDbContext> _integrationService;
        private readonly ILoggerFactory _logger;
        public TenantPropertiesChangedDomainEventHandler(ILoggerFactory logger, IIntegrationEventService<TenantStoreDbContext> integrationService)
        {
            _logger = logger;
            _integrationService = integrationService;
        }
        public async Task Handle(TenantPropertiesChangedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TenantPropertiesChangedDomainEventHandler>()
            .LogTrace("Tenant with Identifier: {Identifier} has been successfully updated properties",
                notification.TenantIdentifier);

            var integrationEvent = new TenantPropertiesChangedIntegrationEvent(notification.TenantIdentifier);
            await _integrationService.AddAndSaveEventAsync(integrationEvent);
        }
    }
}
