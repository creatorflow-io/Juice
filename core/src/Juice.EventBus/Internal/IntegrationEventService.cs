using Juice.Measurement;
using Juice.MultiTenant;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Internal
{
    internal class IntegrationEventService<TContext> : IIntegrationEventService<TContext>
    {
        private readonly IOutboxRepository _eventLogService;
        private readonly ILogger _logger;
        private readonly IEventBus _eventBus;
        private readonly ITenantAccessor? _tenantAccessor;
        private readonly ITimeTracker? _timeTracker;
        private readonly IList<IIntegrationEvent> _events = [];

        public IntegrationEventService(IOutboxRepository<TContext> eventLogService
            , IEventBus eventBus
            , ILogger<IntegrationEventService<TContext>> logger
            , ITenantAccessor? tenantAccessor = default
            , ITimeTracker? timeTracker = default
            )
        {
            _eventLogService = eventLogService;
            _logger = logger;
            _eventBus = eventBus;
            _tenantAccessor = tenantAccessor;
            _timeTracker = timeTracker;
        }

        public ValueTask AddEventAsync(IIntegrationEvent evt)
        {
            if (_logger.IsEnabled(LogLevel.Debug)) {
                _logger.LogDebug("----- Adding {EventType} integration events to repository", evt.GetType());
            }
            _events.Add(evt);
            return ValueTask.CompletedTask;
        }

        public async ValueTask SaveEventsAsync(Guid? transactionId)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("----- Saving {Count} integration events", _events.Count);
            }
            _timeTracker?.BeginScope("Saving integration events");
            await _eventLogService.SaveEventsAsync(transactionId ?? Guid.Empty, [.. _events]);
            _events.Clear();
        }

        public async ValueTask PublishEventsThroughEventBusAsync(Guid transactionId, CancellationToken cancellationToken)
        {
            var pendingEvents = await _eventLogService.RetrieveEventsPendingToPublishAsync(transactionId, cancellationToken);

            await PublishEventsThroughEventBusAsync(pendingEvents, cancellationToken);
        }

        public async ValueTask PublishEventsThroughEventBusAsync(int maxEvents, int maxRetries, CancellationToken cancellationToken = default)
        {
            if (maxEvents <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEvents));
            }
            var pendingEvents = await _eventLogService.RetrieveEventsPendingToPublishAsync(maxEvents, maxRetries, cancellationToken);

            await PublishEventsThroughEventBusAsync(pendingEvents, cancellationToken);
        }

        private async Task PublishEventsThroughEventBusAsync(IEnumerable<IIntegrationEvent> events, CancellationToken cancellationToken)
        {
            foreach (var evt in events)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("----- Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", evt.Id, evt);
                }
                try
                {
                    await _eventLogService.MarkEventAsInProgressAsync(evt.Id, cancellationToken);
                    await _eventBus.PublishAsync(evt, _tenantAccessor?.Tenant?.Id, cancellationToken);
                    await _eventLogService.MarkEventAsPublishedAsync(evt.Id, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // do NOT mark failed
                    // do NOT mark published
                    _logger.LogInformation(
                        "Publishing integration event {EventId} canceled.", evt.Id);

                    throw; // allow host to stop correctly
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ERROR publishing integration event: {IntegrationEventId}. TenantId: {tenantId}, TenantIdentifier: {tenantIdentifier}. {Message}",
                        evt.Id, _tenantAccessor?.Tenant?.Id, _tenantAccessor?.Tenant?.Identifier, ex.Message);
                    _logger.LogTrace(ex, "ERROR publishing integration event: {IntegrationEventId}. {Trace}", evt.Id, ex.StackTrace);
                    await _eventLogService.MarkEventAsFailedAsync(evt.Id, ex.Message, cancellationToken);
                }
            }
        }

    }

    internal class IntegrationEventService<TContext, TEventBus> : IntegrationEventService<TContext>, IIntegrationEventService<TContext, TEventBus>
        where TEventBus : IEventBus
    {
        
        public IntegrationEventService(IOutboxRepository<TContext> eventLogService
            , TEventBus eventBus
            , ILogger<IntegrationEventService<TContext, TEventBus>> logger
            , ITenantAccessor? tenantAccessor = default
            ) : base(eventLogService, eventBus, logger, tenantAccessor)
        {
        }

    }

}
