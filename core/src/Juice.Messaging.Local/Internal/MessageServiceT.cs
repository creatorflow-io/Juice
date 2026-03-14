using System.Threading.Channels;
using Juice.Messaging.Integrations;
using Juice.Messaging.Outbox;
using Juice.MultiTenant;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Local.Internal
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
    /// When called inside an active <c>TransactionBehavior</c> scope,
    /// <c>SaveEventsAsync</c> joins the ambient transaction; the behavior's final
    /// <c>SaveEventsAsync</c> call finds no pending messages (cleared after first save)
    /// and becomes a no-op.
    /// </para>
    /// </summary>
    internal sealed class MessageService<TContext> : MessageService, IMessageService<TContext>
        where TContext : class
    {
        private readonly IOutboxService<TContext>? _outboxService;

        public MessageService(
            Policies.IMessagePublishingPolicy policy,
            ChannelWriter<IMessage> channelWriter,
            ILogger<MessageService<TContext>> logger,
            IOutboxService<TContext>? outboxService = null,
            ITenantAccessor? tenantAccessor = null)
            : base(policy, channelWriter, logger, tenantAccessor)
        {
            _outboxService = outboxService;
        }

        public override async Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
        {
            var routes = await ResolveRoutesAsync(message);

            bool hasLocalChannel = false;
            bool hasLocalOutbox = false;
            bool hasOutboxRoutes = false;

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

            if (hasLocalChannel)
            {
                EnqueueLocalChannel(message);
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
                // OutboxEventService resolves routes internally and skips "local-channel".
                // Passing transactionId=null: when inside an ambient transaction the
                // OutboxRepository joins it; when outside, a standalone write is committed.
                await _outboxService.SaveEventsAsync(null, cancellationToken);

                // For "local" routes, enqueue to the in-memory channel for immediate
                // best-effort dispatch. The outbox entry remains for durability —
                // DeliveryHostedService will pick it up on retry if this dispatch fails.
                // IntegrationEventDispatcher idempotency deduplicates if both succeed.
                if (hasLocalOutbox && !hasLocalChannel)
                {
                    EnqueueLocalChannel(message);
                }
            }
        }
    }
}
