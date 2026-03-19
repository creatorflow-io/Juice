using Juice.Messaging.Extensions;
using Juice.Messaging.Integrations;
using Juice.Messaging.Outbox;
using Juice.Messaging.Policies;
using Juice.Messaging.Publishing;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Internal
{
    /// <summary>
    /// Scoped implementation of <see cref="IMessageService{TContext}"/> that handles all
    /// route types: <c>"local-channel"</c> (in-memory, zero DB), <c>"local"</c>
    /// (outbox-backed, durable), and broker routes.
    /// <para>
    /// For <c>"local-channel"</c> routes the message is enqueued directly to the in-memory
    /// channel. For <c>"local"</c> routes the message is written to the outbox for durability
    /// and then enqueued to the in-memory channel for immediate best-effort dispatch;
    /// <see cref="IntegrationEventDispatcher"/> idempotency ensures that if the immediate
    /// dispatch succeeds, the <c>DeliveryHostedService</c> retry is deduplicated.
    /// For broker routes the message is written to the outbox only.
    /// </para>
    /// <para>
    /// <c>"local-channel"</c> and <c>"local"</c> are mutually exclusive — if the policy
    /// resolves both, <c>"local"</c> takes precedence (durable superset) and the
    /// <c>"local-channel"</c> route is ignored to prevent double handler invocation.
    /// </para>
    /// <para>
    /// When called inside a managed <c>TransactionBehavior</c> scope (detected via
    /// <c>IUnitOfWork.IsManaged</c>), only <c>AddEventAsync</c> is called — the save
    /// is deferred to <c>TransactionBehavior</c>'s <c>SaveEventsAsync(transactionId)</c>,
    /// ensuring atomicity with domain data. Immediate channel dispatch for <c>"local"</c>
    /// routes is also suppressed (data not yet committed).
    /// </para>
    /// <para>
    /// When called outside a managed transaction, <c>SaveEventsAsync(null)</c> is called
    /// immediately as a standalone operation, and <c>"local"</c> routes are enqueued to
    /// the channel for immediate dispatch.
    /// </para>
    /// </summary>
    internal sealed class MessageService<TContext> : IMessageService<TContext>
        where TContext : class
    {
        private readonly IOutboxService<TContext>? _outboxService;
        private readonly TContext? _context;
        private readonly IPostCommitActions? _postCommitActions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessagePublishingPolicy _policy;
        private readonly ITenantAccessor? _tenantAccessor;
        private readonly ILogger _logger;

        public MessageService(
            IServiceProvider serviceProvider,
            Policies.IMessagePublishingPolicy policy,
            ILogger<MessageService<TContext>> logger,
            IOutboxService<TContext>? outboxService = null,
            TContext? context = null,
            IPostCommitActions? postCommitActions = null,
            ITenantAccessor? tenantAccessor = null)
        {
            _outboxService = outboxService;
            _context = context;
            _postCommitActions = postCommitActions;
            _serviceProvider = serviceProvider;
            _policy = policy;
            _tenantAccessor = tenantAccessor;
            _logger = logger;
        }

        public async Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
        {
            var routes = await ResolveRoutesAsync(message);

            bool hasLocalOutbox = false;
            bool hasOutboxRoutes = false;
            bool hasLocalChannel = false;

            foreach (var route in routes)
            {
                if (route.PublisherKey == "local-channel")
                    hasLocalChannel = true;
                else
                {
                    hasOutboxRoutes = true;
                    if (route.PublisherKey == "local")
                        hasLocalOutbox = true;
                }
            }

            // "local" supersedes "local-channel" — they are mutually exclusive.
            // If both are present, "local" wins (durable superset) to prevent
            // double handler invocation.
            if (hasLocalChannel && !hasLocalOutbox)
            {
                await PublishLocalMessageAsync(message, cancellationToken);
            }

            if (hasOutboxRoutes)
            {
                if (_outboxService == null)
                {
                    _logger.LogError("Message {MessageType} has outbox routes but no IOutboxService<{Context}> is registered", message.GetType(), typeof(TContext));
                    throw new InvalidOperationException($"Message {message.GetType()} has outbox routes but no IOutboxService<{typeof(TContext)}> is registered");
                }
                if (!MessageContext.IsInitialized)
                {
                    _logger.LogError("MessageContext is not initialized. An ambient MessageContext scope is required to publish messages with outbox routes.");
                    throw new InvalidOperationException("MessageContext is not initialized. An ambient MessageContext scope is required to publish messages with outbox routes.");
                }
                await _outboxService.AddEventAsync(message);

                var isManaged = _context is IManagable { IsManaged: true };

                if (isManaged)
                {
                    // Inside a managed TransactionBehavior scope — defer save.
                    // TransactionBehavior will call SaveEventsAsync(transactionId) later,
                    // persisting all accumulated events atomically with domain data.
                    // Register a post-commit action to enqueue to channel after commit
                    // for immediate local dispatch.
                    if (hasLocalOutbox && _postCommitActions != null)
                    {
                        _postCommitActions.Add(async () => await PublishLocalMessageAsync(message, CancellationToken.None));
                    }
                    return;
                }

                // Outside transaction — save immediately as standalone operation.
                await _outboxService.SaveEventsAsync(null, cancellationToken);

                // For "local" routes, enqueue to the in-memory channel for immediate
                // best-effort dispatch. The outbox entry remains for durability —
                // DeliveryHostedService will pick it up on retry if this dispatch fails.
                // IntegrationEventDispatcher idempotency deduplicates if both succeed.
                if (hasLocalOutbox)
                {
                    await PublishLocalMessageAsync(message, cancellationToken);
                }
            }
        }


        private async Task<IReadOnlyCollection<PublishRoute>> ResolveRoutesAsync(IMessage message)
        {
            return await _policy.ResolveAsync(new PolicyResolveContext
            {
                Domain = message.GetType().GetDomainName(),
                EventType = message.GetType().Name,
                TenantIdentifier = _tenantAccessor?.Tenant?.Identifier,
                TenantTier = _tenantAccessor?.Tenant?.Tier
            });
        }

        private async Task PublishLocalMessageAsync(IMessage message, CancellationToken cancellationToken)
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
