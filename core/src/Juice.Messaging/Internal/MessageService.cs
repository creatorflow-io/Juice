using Juice.Messaging.Extensions;
using Juice.Messaging.Policies;
using Juice.Messaging.Publishing;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Internal
{
    /// <summary>
    /// Scoped implementation of <see cref="IMessageService"/> that handles
    /// <c>"local-channel"</c> routes only. Messages routed to <c>"local"</c> or broker
    /// publisher keys are silently ignored — callers that need those routes must use
    /// <see cref="IMessageService{TContext}"/> instead.
    /// </summary>
    internal class MessageService : IMessageService
    {
        protected readonly IServiceProvider _serviceProvider;
        protected readonly IMessagePublishingPolicy _policy;
        protected readonly ITenantAccessor? _tenantAccessor;
        protected readonly ILogger _logger;

        public MessageService(
            IServiceProvider serviceProvider,
            IMessagePublishingPolicy policy,
            ILogger<MessageService> logger,
            ITenantAccessor? tenantAccessor = null)
        {
            _serviceProvider = serviceProvider;
            _policy = policy;
            _logger = logger;
            _tenantAccessor = tenantAccessor;
        }

        /// <summary>
        /// Internal constructor for derived classes that provide their own logger.
        /// </summary>
        protected MessageService(
            IServiceProvider serviceProvider,
            IMessagePublishingPolicy policy,
            ILogger logger,
            ITenantAccessor? tenantAccessor)
        {
            _serviceProvider = serviceProvider;
            _policy = policy;
            _logger = logger;
            _tenantAccessor = tenantAccessor;
        }

        public virtual async Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
        {
            var routes = await ResolveRoutesAsync(message);
            var hasLocalRoutes = routes.Any(r => r.PublisherKey == "local");
            if (hasLocalRoutes)
            {
                return; // local-channel routes are ignored if local (outbox) routes are present, to prevent double dispatch
            }
            var hasLocalChannelRoutes = routes.Any(r => r.PublisherKey == "local-channel");
            if (hasLocalChannelRoutes)
            {
                await PublishLocalMessageAsync(message, cancellationToken);
            }
        }

        protected async Task<IReadOnlyCollection<PublishRoute>> ResolveRoutesAsync(IMessage message)
        {
            return await _policy.ResolveAsync(new PolicyResolveContext
            {
                Domain = message.GetType().GetDomainName(),
                EventType = message.GetType().Name,
                TenantIdentifier = _tenantAccessor?.Tenant?.Identifier,
                TenantTier = _tenantAccessor?.Tenant?.Tier
            });
        }

        protected async Task PublishLocalMessageAsync(IMessage message, CancellationToken cancellationToken)
        {
            var contextSnapshot = MessageContext.IsInitialized
                ? MessageContext.Current
                : null;
            using var scope = _serviceProvider.CreateScope();
            var messagePublisher = scope.ServiceProvider.GetKeyedService<IMessagePublisher>("local-channel");
            if (messagePublisher == null)
            {
                _logger.LogWarning("No local-channel publisher registered; message {MessageId} will not be dispatched to handlers", message.MessageId);
                return;
            }
            await messagePublisher.PublishAsync(message, contextSnapshot, cancellationToken);
        }
    }
}
