using Juice.EventBus.Extensions;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Dispatching
{
    public sealed class IntegrationEventDispatcher
    {
        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        public IntegrationEventDispatcher(
            ILogger<IntegrationEventDispatcher> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }
        public async Task<(bool Handled, bool Succeeded)> DispatchAsync(
            IIntegrationEvent evt, EventDispatchContext context)
        {
            var eventName = context.EventName ?? evt.GetType().Name;
            var eventId = evt.Id;
            var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(evt.GetType());
            using var scope = _scopeFactory.CreateScope();
            var tenantResolver = scope.ServiceProvider.GetService<IScopedTenantResolver>();
            using var _1 = tenantResolver?.Resolve(context.TenantId);
            if(_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Dispatching event: {EventName}, eventId: {eventId} to {HandlerCount} handlers for tenant: {TenantId}"
                    ,eventName, eventId, context.Handlers.Count(), context.TenantId);
            }
            bool ok = false, handled = false;
            foreach (var handlerType in context.Handlers)
            {
                if (!handlerType.IsAssignableTo(concreteType))
                {
                    _logger.LogWarning("Type {typeName} not assignable to {concreteType}", handlerType.Name, concreteType.Name);

                    continue;
                }
                var handler = scope.ServiceProvider.GetService(handlerType);
                if (handler == null)
                {
                    _logger.LogWarning("Type {typeName} not registered as a service", handlerType.Name);

                    continue;
                }

                try
                {
                    handled = true;
                    await (Task)concreteType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!.Invoke(handler, new object[] { evt })!;
                    ok = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{handler} failed to handle event: {EventName}, eventId: {eventId}", handler.GetGenericTypeName(), eventName, eventId);
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace(ex, "Event: {EventName}, eventId: {eventId} exception stack trace: {StackTrace}", eventName, eventId, ex.StackTrace);
                    }
                }
            }
            return (handled, ok);
        }
    }
}
