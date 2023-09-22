using Juice.Integrations.EventBus;
using Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events;
using Juice.MultiTenant.Domain.Events;
using Juice.MultiTenant.EF;

namespace Juice.MultiTenant.Api.Domain.EventHandlers
{
    internal class TenantDeletedDomainEventHandler : INotificationHandler<TenantDeletedDomainEvent>
    {
        private IIntegrationEventService<TenantStoreDbContext> _integrationService;
        private readonly ILoggerFactory _logger;
        public TenantDeletedDomainEventHandler(ILoggerFactory logger, IIntegrationEventService<TenantStoreDbContext> integrationService)
        {
            _logger = logger;
            _integrationService = integrationService;
        }
        public async Task Handle(TenantDeletedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TenantDeletedDomainEventHandler>()
            .LogTrace("Tenant with Identifier: {Identifier} has been successfully deleted",
                notification.TenantIdentifier);

            var integrationEvent = new TenantDeletedIntegrationEvent(notification.TenantIdentifier, notification.TenantName);
            await _integrationService.AddAndSaveEventAsync(integrationEvent);
        }
    }
}
