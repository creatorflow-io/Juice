using Juice.EventBus;

namespace Juice.Tests.Host.IntegrationEvents
{
    public class TenantSettingsChangedIntegrationEventHandler
        : IIntegrationEventHandler<TenantSettingsChangedIntegrationEvent>
    {
        public Task HandleAsync(TenantSettingsChangedIntegrationEvent @event)
        {
            Console.WriteLine("Ooh tenant {0} settings was changed!", @event.TenantIdentifier);
            return Task.CompletedTask;
        }
    }
}
