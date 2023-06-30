using Juice.EventBus;
using Juice.Integrations.EventBus;
using Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events;
using Juice.MultiTenant.Domain.Events;
using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.Domain.EventHandlers
{
    internal class TenantStatusChangedDomainEventHandler : INotificationHandler<TenantStatusChangedDomainEvent>
    {
        private IIntegrationEventService<TenantStoreDbContext> _integrationService;
        private readonly ILoggerFactory _logger;
        public TenantStatusChangedDomainEventHandler(ILoggerFactory logger, IIntegrationEventService<TenantStoreDbContext> integrationService)
        {
            _logger = logger;
            _integrationService = integrationService;
        }
        public async Task Handle(TenantStatusChangedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TenantStatusChangedDomainEventHandler>()
            .LogTrace("Tenant with Identifier: {Identifier} has been successfully activated",
                notification.TenantIdentifier);

            IntegrationEvent? integrationEvent =
                notification.TenantStatus switch
                {
                    TenantStatus.Initializing => new TenantInitializationChangedIntegrationEvent(notification.TenantIdentifier, notification.TenantStatus),
                    TenantStatus.Initialized => new TenantInitializationChangedIntegrationEvent(notification.TenantIdentifier, notification.TenantStatus),
                    TenantStatus.Approved => new TenantApprovalChangedIntegrationEvent(notification.TenantIdentifier, notification.TenantStatus),
                    TenantStatus.PendingApproval => new TenantApprovalChangedIntegrationEvent(notification.TenantIdentifier, notification.TenantStatus),
                    TenantStatus.Rejected => new TenantApprovalChangedIntegrationEvent(notification.TenantIdentifier, notification.TenantStatus),
                    TenantStatus.Active => new TenantActivatedIntegrationEvent(notification.TenantIdentifier),
                    TenantStatus.Inactive => new TenantDeactivatedIntegrationEvent(notification.TenantIdentifier),
                    TenantStatus.PendingToActive => new TenantRequestActiveIntegrationEvent(notification.TenantIdentifier),
                    TenantStatus.Suspended => new TenantSuspendedIntegrationEvent(notification.TenantIdentifier),
                    TenantStatus.Abandoned => new TenantAbandonedIntegrationEvent(notification.TenantIdentifier),
                    _ => default
                };
            if (integrationEvent != null)
            {
                await _integrationService.AddAndSaveEventAsync(integrationEvent);
            }
        }
    }
}
