using Juice.EventBus;

namespace Juice.Tests.Host.IntegrationEvents
{
    public class TenantActivatedIntegrationEventHandler : IIntegrationEventHandler<TenantActivatedIntegrationEvent>
    {
        public Task HandleAsync(TenantActivatedIntegrationEvent @event)
        {
            Console.WriteLine("Ooh tenant {0} activated!", @event.TenantIdentifier);
            return Task.CompletedTask;
        }
    }
}
