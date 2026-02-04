using Juice.EventBus.Publishing;
using Juice.EventBus.Publishing.Policies;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.EventBus.Internal
{
    internal class CompositeEventPublisher : IEventBus
    {
        private readonly IEventPublishingPolicy _policy;
        private readonly ITenantAccessor? _tenantAccessor;
        private readonly IServiceProvider _serviceProvider;

        public CompositeEventPublisher(
            IEventPublishingPolicy policy,
            IServiceProvider serviceProvider,
            ITenantAccessor? tenantAccessor = null
            )
        {
            _policy = policy;
            _tenantAccessor = tenantAccessor;
            _serviceProvider = serviceProvider;
        }

        public async ValueTask PublishAsync<T>(
            T @event, string? domain = default,
            CancellationToken ct = default)
            where T : IIntegrationEvent
        {
            var routes = await _policy.ResolveAsync(new PolicyResolveContext
            {
                Domain = @event.Domain ?? domain,
                EventType = @event.GetType().Name,
                TenantIdentifier = _tenantAccessor?.Tenant?.Identifier,
                TenantTier = _tenantAccessor?.Tenant?.Tier
            });

            foreach (var route in routes)
            {
                var publisher = _serviceProvider.GetKeyedService<IEventPublisher>(route.PublisherKey);
                if (publisher is null)
                {
                    throw new InvalidOperationException(
                        $"Publisher '{route.PublisherKey}' not registered");
                }

                await publisher.PublishAsync(
                    @event,
                    new PublishContext { Destination = route.Destination, TenantId = _tenantAccessor?.Tenant?.Id },
                    ct);
            }
        }

        public async ValueTask PublishAsync<T>(
            T @event, string publisherKey, PublishContext context,
            CancellationToken ct = default)
            where T : IIntegrationEvent
        {
            var publisher = _serviceProvider.GetKeyedService<IEventPublisher>(publisherKey);
            if (publisher is null)
            {
                throw new InvalidOperationException(
                    $"Publisher '{publisherKey}' not registered");
            }

            await publisher.PublishAsync(
                @event,
                context,
                ct);
        }
    }
}
