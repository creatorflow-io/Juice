using Juice.Integrations.EventBus;
using Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events;
using Juice.MultiTenant.Domain.Events;
using Juice.MultiTenant.EF;

namespace Juice.MultiTenant.Api.Domain.EventHandlers
{
    internal class TenantSettingsChangedDomainEventHandler : INotificationHandler<TenantSettingsChangedDomainEvent>
    {
        private IIntegrationEventService<TenantSettingsDbContext> _integrationService;
        private readonly ILoggerFactory _logger;

        public TenantSettingsChangedDomainEventHandler(IIntegrationEventService<TenantSettingsDbContext> integrationService, ILoggerFactory logger)
        {
            _integrationService = integrationService;
            _logger = logger;
        }

        public async Task Handle(TenantSettingsChangedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<TenantDeactivatedDomainEventHandler>()
            .LogTrace("Tenant settings with Identifier: {Identifier} has been successfully updated.",
                notification.TenantIdentifier);

            var integrationEvent = new TenantSettingsChangedIntegrationEvent(notification.TenantIdentifier);
            await _integrationService.AddAndSaveEventAsync(integrationEvent);
        }
    }
}
