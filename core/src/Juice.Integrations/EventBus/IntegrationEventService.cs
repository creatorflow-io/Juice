using Juice.EF.Extensions;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.MultiTenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.EventBus
{
    internal class IntegrationEventService<TContext, TEventBus> : IIntegrationEventService<TContext>
        where TContext : DbContext
        where TEventBus : IEventBus
    {
        private IIntegrationEventLogService<TContext> _eventLogService;
        public TContext DomainContext { get; }
        private readonly ILogger _logger;
        private readonly TEventBus _eventBus;
        private readonly ITenantAccessor? _tenantAccessor;
        public IntegrationEventService(IIntegrationEventLogService<TContext> eventLogService
            , TContext domainContext
            , TEventBus eventBus
            , ILogger<IntegrationEventService<TContext, TEventBus>> logger
            , ITenantAccessor? tenantAccessor = default
            )
        {
            _eventLogService = eventLogService;
            DomainContext = domainContext;
            _logger = logger;
            _eventBus = eventBus;
            _tenantAccessor = tenantAccessor;
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
                    if(logEvt.IntegrationEvent is null)
                    {
                        _logger.LogError("Integration event is null. EventId: {IntegrationEventId}", logEvt.EventId);
                        await _eventLogService.MarkEventAsFailedAsync(logEvt.EventId);
                        continue;
                    }
                    await _eventBus.PublishAsync(logEvt.IntegrationEvent, _tenantAccessor?.Tenant?.Id);
                    await _eventLogService.MarkEventAsPublishedAsync(logEvt.EventId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ERROR publishing integration event: {IntegrationEventId}. TenantId: {tenantId}, TenantIdentifier: {tenantIdentifier}. {Message}",
                        logEvt.EventId, _tenantAccessor?.Tenant?.Id, _tenantAccessor?.Tenant?.Identifier, ex.Message);
                    _logger.LogTrace(ex, "ERROR publishing integration event: {IntegrationEventId}. {Trace}", logEvt.EventId, ex.StackTrace);
                    await _eventLogService.MarkEventAsFailedAsync(logEvt.EventId);
                }
            }
        }
    }
}
