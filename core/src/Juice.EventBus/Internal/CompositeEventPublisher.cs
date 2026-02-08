using Juice.EventBus.Publishing;
using Juice.Messaging;
using Juice.Messaging.Policies;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.EventBus.Internal
{
    internal class CompositeEventPublisher : IEventBus
    {
        private readonly IMessagePublishingPolicy _policy;
        private readonly IMessageSerializer _serializer;
        private readonly ITenantAccessor? _tenantAccessor;
        private readonly IServiceProvider _serviceProvider;

        public CompositeEventPublisher(
            IMessagePublishingPolicy policy,
            IMessageSerializer serializer,
            IServiceProvider serviceProvider,
            ITenantAccessor? tenantAccessor = null
            )
        {
            _policy = policy;
            _serializer = serializer;
            _tenantAccessor = tenantAccessor;
            _serviceProvider = serviceProvider;
        }

        public async ValueTask PublishAsync<T>(
            T @event, string? domain = default,
            CancellationToken ct = default)
            where T : IMessage
        {
            var routes = await _policy.ResolveAsync(new PolicyResolveContext
            {
                Domain = domain,
                EventType = @event.GetType().Name,
                TenantIdentifier = _tenantAccessor?.Tenant?.Identifier,
                TenantTier = _tenantAccessor?.Tenant?.Tier
            });

            foreach (var route in routes)
            {
                var publisher = _serviceProvider.GetKeyedService<ITransportPublisher>(route.PublisherKey);
                if (publisher is null)
                {
                    throw new InvalidOperationException(
                        $"Publisher '{route.PublisherKey}' not registered");
                }

                await publisher.PublishAsync(
                    _serializer.SerializeToUtf8Bytes(@event),
                    new PublishContext(@event.MessageId.ToString()) {
                        Destination = route.Destination, TenantId = _tenantAccessor?.Tenant?.Id,
                        Headers = new Dictionary<string, object?>
                        {
                            { "x-tenant-id", _tenantAccessor?.Tenant?.Id },
                            { "x-message-type", @event.GetType().Name },
                            { "x-message-name", (@event as IEvent)?.EventName ?? @event.GetType().Name },
                            { "x-correlation-id", MessageContext.Current.CorrelationId },
                            { "x-causation-id", MessageContext.Current.ExecutionId }
                        }
                    },
                    ct);
            }
        }

        public async ValueTask PublishAsync<T>(
            T @event, string publisherKey, PublishContext context,
            CancellationToken ct = default)
            where T : IMessage
        {
            var publisher = _serviceProvider.GetKeyedService<ITransportPublisher>(publisherKey);
            if (publisher is null)
            {
                throw new InvalidOperationException(
                    $"Publisher '{publisherKey}' not registered");
            }

            var headers = context.Headers ?? new Dictionary<string, object?>();
            headers["x-tenant-id"] = context.TenantId ?? _tenantAccessor?.Tenant?.Id;
            headers["x-message-type"] = @event.GetType().Name;
            headers["x-event-name"] = (@event as IEvent)?.EventName ?? @event.GetType().Name;
            headers["x-correlation-id"] = MessageContext.Current.CorrelationId;
            headers["x-causation-id"] = MessageContext.Current.ExecutionId;

            await publisher.PublishAsync(
                _serializer.SerializeToUtf8Bytes(@event),
                new PublishContext(context.MessageId) {
                    Destination = context.Destination,
                    TenantId = context.TenantId ?? _tenantAccessor?.Tenant?.Id,
                    Headers = headers
                },
                ct);
        }
    }
}
