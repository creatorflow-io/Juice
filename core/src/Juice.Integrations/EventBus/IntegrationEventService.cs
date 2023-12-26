using Juice.EF.Extensions;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.EventBus
{
    internal class IntegrationEventService<TContext> : IIntegrationEventService<TContext>
        where TContext : DbContext
    {
        private IIntegrationEventLogService<TContext> _eventLogService;
        public TContext DomainContext { get; }
        private readonly ILogger _logger;
        private readonly IEventBus _eventBus;
        public IntegrationEventService(IIntegrationEventLogService<TContext> eventLogService
            , TContext domainContext
            , IEventBus eventBus
            , ILogger<IntegrationEventService<TContext>> logger
            )
        {
            _eventLogService = eventLogService;
            DomainContext = domainContext;
            _logger = logger;
            _eventBus = eventBus;
        }

        public async Task AddAndSaveEventAsync(IntegrationEvent evt, IDbContextTransaction? transaction = default)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("----- Enqueuing integration event {IntegrationEventId} to repository ({@IntegrationEvent})", evt.Id, evt);
            }
            transaction = transaction ?? DomainContext.GetCurrentTransaction();
            if (transaction == null)
            {
                throw new Exception($"{typeof(TContext).Name} does not have an active transaction");
            }
            _eventLogService.EnsureAssociatedConnection(DomainContext);
            await _eventLogService.SaveEventAsync(evt, transaction);
        }
        public async Task PublishEventsThroughEventBusAsync(Guid transactionId)
        {
            var pendingLogEvents = await _eventLogService.RetrieveEventLogsPendingToPublishAsync(transactionId);

            foreach (var logEvt in pendingLogEvents)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("----- Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", logEvt.EventId, logEvt.IntegrationEvent);
                }
                try
                {
                    await _eventLogService.MarkEventAsInProgressAsync(logEvt.EventId);
                    await _eventBus.PublishAsync(logEvt.IntegrationEvent);
                    await _eventLogService.MarkEventAsPublishedAsync(logEvt.EventId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ERROR publishing integration event: {IntegrationEventId}. {Message}", logEvt.EventId, ex.Message);
                    _logger.LogTrace(ex, "ERROR publishing integration event: {IntegrationEventId}. {Trace}", logEvt.EventId, ex.StackTrace);
                    await _eventLogService.MarkEventAsFailedAsync(logEvt.EventId);
                }
            }
        }
    }
}
