using Juice.EventBus.Publishing;
using Juice.Messaging;
using Juice.Messaging.Context;
using Juice.Messaging.Extensions;
using Juice.Messaging.Policies;
using Juice.Messaging.Publishing;
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
                Domain = domain ?? @event.GetType().GetDomainName(),
                EventType = @event.GetType().Name,
                TenantIdentifier = _tenantAccessor?.Tenant?.Identifier,
                TenantTier = _tenantAccessor?.Tenant?.Tier
            });
            if (!MessageContext.IsInitialized)
            {
                throw new InvalidOperationException(
                    "MessageContext must be initialized before publishing via IEventBus. " +
                    "Add UseMessageContext() middleware or the [InitializeMessageContext] attribute at the entry point.");
            }
            var ctx = MessageContext.Current;
            foreach (var route in routes)
            {
                await PublishAsync(@event, route.PublisherKey, new PublishContext(@event.MessageId.ToString())
                {
                    Destination = route.Destination,
                    TenantId = _tenantAccessor?.Tenant?.Id,
                    RoutingKey = route.RoutingKey
                }, ctx, ct);
            }
        }

        private async ValueTask PublishAsync<T>(
            T message, string publisherKey, PublishContext context, MessageContextData ctx,
            CancellationToken ct = default)
            where T : IMessage
        {
            var publisher = _serviceProvider.GetKeyedService<ITransportPublisher>(publisherKey);
            if (publisher is null)
            {
                throw new InvalidOperationException(
                    $"Publisher '{publisherKey}' not registered");
            }
            var headers = new Dictionary<string, object?>
                            {
                                { "x-correlation-id", ctx.CorrelationId},
                                { "x-causation-id", ctx.ExecutionId },
                                { "x-source", ctx.Source},
                                { "x-tenant-id", _tenantAccessor?.Tenant?.Id },
                                { "x-message-type", message.GetType().Name },
                                { "x-message-clr-type", $"{message.GetType().FullName}, {message.GetType().Assembly.GetName().Name}" },
                                { "x-message-id", message.MessageId},
                                { "x-message-name", message is IEvent evt ? evt.EventName : message.GetType().Name },
                            };
            if (!string.IsNullOrEmpty(context.RoutingKey))
            {
                headers["x-routing-key"] = context.RoutingKey;
            }

            await publisher.PublishAsync(
                _serializer.SerializeToUtf8Bytes(message),
                context.WithHeaders(headers),
                ct);
        }

        public ValueTask PublishAsync<T>(
            T @event, string publisherKey, PublishContext context,
            CancellationToken ct = default)
            where T : IMessage
        {
            if (!MessageContext.IsInitialized)
            {
                throw new InvalidOperationException(
                    "MessageContext must be initialized before publishing via IEventBus. " +
                    "Add UseMessageContext() middleware or the [InitializeMessageContext] attribute at the entry point.");
            }
            return PublishAsync(@event, publisherKey, context, MessageContext.Current, ct);
        }
    }
}
