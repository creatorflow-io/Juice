using Juice.Integrations.EventBus;
using Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events;
using Juice.MultiTenant.Domain.Events;

namespace Juice.MultiTenant.Api.Domain.EventHandlers
{
    internal class TenantOwnerChangedDomainEventHandler : INotificationHandler<TenantOwnerChangedDomainEvent>
    {
        private IIntegrationEventService<TenantStoreDbContext> _integrationService;
        private readonly ILoggerFactory _logger;
        public TenantOwnerChangedDomainEventHandler(ILoggerFactory logger, IIntegrationEventService<TenantStoreDbContext> integrationService)
        {
            _logger = logger;
            _integrationService = integrationService;
        }
        public async Task Handle(TenantOwnerChangedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TenantStatusChangedDomainEventHandler>()
            .LogTrace("Tenant with Identifier: {Identifier} has been changed the owner",
                notification.TenantIdentifier);

            var integrationEvent = new TenantOwnerChangedIntegrationEvent(
                notification.TenantIdentifier ?? notification.TenantId,
                notification.FromUser, notification.ToUser);

            await _integrationService.AddAndSaveEventAsync(integrationEvent);
        }
    }
}
