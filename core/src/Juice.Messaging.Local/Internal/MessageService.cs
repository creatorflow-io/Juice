using System.Threading.Channels;
using Juice.Messaging.Extensions;
using Juice.Messaging.Policies;
using Juice.MultiTenant;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Local.Internal
{
    /// <summary>
    /// Scoped implementation of <see cref="IMessageService"/> that handles
    /// <c>"local-channel"</c> routes only. Messages routed to <c>"local"</c> or broker
    /// publisher keys are silently ignored — callers that need those routes must use
    /// <see cref="IMessageService{TContext}"/> instead.
    /// </summary>
    internal class MessageService : IMessageService
    {
        protected readonly IMessagePublishingPolicy _policy;
        protected readonly ChannelWriter<ChannelEnvelope> _channelWriter;
        protected readonly ITenantAccessor? _tenantAccessor;
        protected readonly ILogger _logger;

        public MessageService(
            IMessagePublishingPolicy policy,
            ChannelWriter<ChannelEnvelope> channelWriter,
            ILogger<MessageService> logger,
            ITenantAccessor? tenantAccessor = null)
        {
            _policy = policy;
            _channelWriter = channelWriter;
            _logger = logger;
            _tenantAccessor = tenantAccessor;
        }

        /// <summary>
        /// Internal constructor for derived classes that provide their own logger.
        /// </summary>
        protected MessageService(
            IMessagePublishingPolicy policy,
            ChannelWriter<ChannelEnvelope> channelWriter,
            ILogger logger,
            ITenantAccessor? tenantAccessor)
        {
            _policy = policy;
            _channelWriter = channelWriter;
            _logger = logger;
            _tenantAccessor = tenantAccessor;
        }

        public virtual async Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
        {
            var routes = await ResolveRoutesAsync(message);
            foreach (var route in routes)
            {
                if (route.PublisherKey == "local-channel")
                {
                    EnqueueLocalChannel(message);
                }
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

        protected void EnqueueLocalChannel(IMessage message)
        {
            // Capture MessageContext snapshot so the background service can restore
            // it before dispatch — ensures consistent idempotency keys across
            // the channel dispatch and DeliveryHostedService retry paths.
            var contextSnapshot = MessageContext.IsInitialized
                ? MessageContext.Current
                : null;
            var envelope = new ChannelEnvelope(message, contextSnapshot);
            if (!_channelWriter.TryWrite(envelope))
            {
                _logger.LogWarning("Failed to enqueue {MessageType} to local-channel",
                    message.GetType().Name);
            }
        }
    }
}
