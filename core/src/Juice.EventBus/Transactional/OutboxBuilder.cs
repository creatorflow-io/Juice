using Juice.EventBus.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.EventBus.Transactional
{
    public sealed class OutboxBuilder
    {
        private readonly IServiceCollection _services;
        public IServiceCollection Services => _services;
        internal OutboxBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public void AddDefaultServices()
        {
            // Register IntegrationEventService
            _services.TryAdd(ServiceDescriptor.Scoped(typeof(IIntegrationEventService<>), typeof(IntegrationEventService<>)));
        }
    }
}
