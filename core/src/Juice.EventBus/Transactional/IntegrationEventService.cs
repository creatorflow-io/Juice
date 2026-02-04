using Juice.EventBus.Delivery;
using Juice.EventBus.Publishing.Policies;
using Juice.Measurement;
using Juice.MultiTenant;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.EventBus.Transactional
{
    internal class IntegrationEventService<TContext> : IIntegrationEventService<TContext>
    {
        private readonly IOutboxRepository _outbox;
        private readonly ILogger _logger;
        private readonly ITimeTracker? _timeTracker;
        private readonly IList<IIntegrationEvent> _events = [];
        private readonly IEventPublishingPolicy _publishingPolicy;
        private readonly ITenantAccessor? _tenantAccessor;

        public IntegrationEventService(IOutboxRepository<TContext> eventLogService
            , IEventPublishingPolicy publishingPolicy
            , ILogger<IntegrationEventService<TContext>> logger
            , ITimeTracker? timeTracker = default
            , ITenantAccessor? tenantAccessor = default
            )
        {
            _outbox = eventLogService;
            _publishingPolicy = publishingPolicy;
            _logger = logger;
            _timeTracker = timeTracker;
            _tenantAccessor = tenantAccessor;
        }

        public ValueTask AddEventAsync(IIntegrationEvent evt)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("----- Adding {EventType} integration events to repository", evt.GetType());
            }
            _events.Add(evt);
            return ValueTask.CompletedTask;
        }

        public async ValueTask SaveEventsAsync(Guid? transactionId, CancellationToken cancellationToken)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("----- Saving {Count} integration events", _events.Count);
            }
            _timeTracker?.BeginScope("Saving integration events");

            var events = new List<OutboxEvent>();
            foreach (var @event in _events)
            {
                var routes = await _publishingPolicy.ResolveAsync(new PolicyResolveContext
                {
                    Domain = @event.Domain ?? typeof(TContext).Name,
                    EventType = @event.GetType().Name,
                    TenantIdentifier = _tenantAccessor?.Tenant?.Identifier,
                    TenantTier = _tenantAccessor?.Tenant?.Tier
                });
                if (routes.Count == 0)
                {
                    continue;
                }
                events.Add(new OutboxEvent
                {
                    EventId = @event.Id,
                    CreationTime = @event.CreationDate,
                    EventTypeName = @event.GetType().Name,
                    Payload = JsonConvert.SerializeObject(@event),
                    TransactionId = transactionId?.ToString(),
                    TenantId = _tenantAccessor?.Tenant?.Id,
                    Deliveries = [.. routes.Select(route => new OutboxDelivery
                    {
                        EventId = @event.Id,
                        PublisherKey = route.PublisherKey,
                        Destination = route.Destination
                    })]
                });
            }
            await _outbox.SaveEventsAsync([.. events], cancellationToken);
            _events.Clear();
        }

    }
}
