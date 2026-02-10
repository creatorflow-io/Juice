using Juice.Measurement;
using Juice.Messaging.Policies;
using Juice.MultiTenant;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Juice.Messaging.Outbox.Internal
{
    internal class OutboxEventService<TContext> : IOutboxService<TContext>
    {
        private readonly IOutboxRepository _outbox;
        private readonly ILogger _logger;
        private readonly ITimeTracker? _timeTracker;
        private readonly IList<IMessage> _messages = [];
        private readonly IMessagePublishingPolicy _publishingPolicy;
        private readonly IMessageSerializer _serializer;
        private readonly ITenantAccessor? _tenantAccessor;

        public OutboxEventService(IOutboxRepository<TContext> eventLogService
            , IMessagePublishingPolicy publishingPolicy
            , IMessageSerializer serializer
            , ILogger<OutboxEventService<TContext>> logger
            , ITimeTracker? timeTracker = default
            , ITenantAccessor? tenantAccessor = default
            )
        {
            _outbox = eventLogService;
            _publishingPolicy = publishingPolicy;
            _serializer = serializer;
            _logger = logger;
            _timeTracker = timeTracker;
            _tenantAccessor = tenantAccessor;
        }

        public ValueTask AddEventAsync(IMessage message)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("----- Adding {EventType} to repository", message.GetType());
            }
            _messages.Add(message);
            return ValueTask.CompletedTask;
        }

        public async ValueTask SaveEventsAsync(Guid? transactionId, CancellationToken cancellationToken)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("----- Saving {Count} integration events", _messages.Count);
            }
            _timeTracker?.BeginScope("Saving integration events");

            var events = new List<OutboxEvent>();
            foreach (var message in _messages)
            {
                var routes = await _publishingPolicy.ResolveAsync(new PolicyResolveContext
                {
                    Domain = typeof(TContext).Name,
                    EventType = message.GetType().Name,
                    TenantIdentifier = _tenantAccessor?.Tenant?.Identifier,
                    TenantTier = _tenantAccessor?.Tenant?.Tier
                });
                if (routes.Count == 0)
                {
                    continue;
                }
                var ctx = MessageContext.Current;
                events.Add(new OutboxEvent
                {
                    EventId = message.MessageId,
                    CreationTime = message.CreatedAt,
                    EventTypeName = message.GetType().Name,
                    PayloadBytes = _serializer.SerializeToUtf8Bytes(message),
                    Headers = new Dictionary<string, object?>
                                    {
                                        { "x-correlation-id", ctx.CorrelationId},
                                        { "x-causation-id", ctx.ExecutionId },
                                        { "x-source", ctx.Source},
                                        { "x-tenant-id", _tenantAccessor?.Tenant?.Id },
                                        { "x-message-type", message.GetType().Name },
                                        { "x-message-id", message.MessageId},
                                        { "x-message-name", message is IEvent evt ? evt.EventName : message.GetType().Name },
                                    },
                    TransactionId = transactionId?.ToString(),
                    TenantId = _tenantAccessor?.Tenant?.Id,
                    Deliveries = [.. routes.Select(route => new OutboxDelivery
                    {
                        EventId = message.MessageId,
                        PublisherKey = route.PublisherKey,
                        Destination = route.Destination
                    })]
                });
            }
            await _outbox.SaveEventsAsync([.. events], cancellationToken);
            _messages.Clear();
        }

    }
}
