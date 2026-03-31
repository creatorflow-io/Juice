using System.Collections.Concurrent;
using System.Reflection;
using Juice.Messaging.Idempotency;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Integrations
{
    public sealed class IntegrationEventDispatcher
    {
        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly ConcurrentDictionary<Type, MethodInfo> _methodCache = new();

        public IntegrationEventDispatcher(
            ILogger<IntegrationEventDispatcher> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<EventDispatchResult> DispatchAsync(
            IIntegrationEvent evt, EventDispatchContext context)
        {
            if(context.Handlers == null || !context.Handlers.Any())
            {
                if(_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("No handlers found for event: {EventName}, eventId: {eventId} for tenant: {TenantId}"
                    , context.EventName, evt.MessageId, context.TenantId);
                return EventDispatchResult.NotHandled;
            }
            using var scope = _scopeFactory.CreateScope();
            var idempotencyService = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
            var create = await idempotencyService.TryCreateRequestAsync(context.EventName, $"{context.Source}:{evt.MessageId}");
            if (!create.Succeeded)
            {
                return EventDispatchResult.Duplicated;
            }
            EventDispatchResult _result = EventDispatchResult.NotHandled;
            try
            {
                var eventName = context.EventName ?? evt.GetType().Name;
                var eventId = evt.MessageId;
                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(evt.GetType()); // resolves Juice.Messaging.IIntegrationEventHandler<>
                var tenantResolver = scope.ServiceProvider.GetService<IScopedTenantResolver>();
                using var _1 = tenantResolver?.Resolve(context.TenantId);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Dispatching event: {EventName}, eventId: {eventId} to {HandlerCount} handlers for tenant: {TenantId}"
                        , eventName, eventId, context.Handlers.Count(), context.TenantId);
                }
                bool ok = false, handled = false;
                foreach (var handlerType in context.Handlers)
                {
                    if (!handlerType.IsAssignableTo(concreteType))
                    {
                        _logger.LogWarning("Type {typeName} not assignable to {concreteType}", handlerType.Name, concreteType.Name);

                        continue;
                    }
                    // Try resolving by concrete type first (e.g. RabbitMQ subscriptions),
                    // then fall back to resolving by interface (e.g. local-channel handlers).
                    var handler = scope.ServiceProvider.GetService(handlerType)
                        ?? scope.ServiceProvider.GetServices(concreteType)
                            .FirstOrDefault(h => h!.GetType() == handlerType);
                    if (handler == null)
                    {
                        _logger.LogWarning("Type {typeName} not registered as a service", handlerType.Name);

                        continue;
                    }

                    try
                    {
                        handled = true;
                        var handleMethod = _methodCache.GetOrAdd(concreteType, type =>
                            type.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!);
                        await (Task)handleMethod.Invoke(handler, new object[] { evt })!;
                        ok = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{handler} failed to handle event: {EventName}, eventId: {eventId}. {Message}", handler.GetType().Name, eventName, eventId, ex.Message);
                        if (_logger.IsEnabled(LogLevel.Trace))
                        {
                            _logger.LogTrace(ex, "Event: {EventName}, eventId: {eventId} exception stack trace: {StackTrace}", eventName, eventId, ex.StackTrace);
                        }
                    }
                }
                _result =  ok ? EventDispatchResult.Success : handled ? EventDispatchResult.Failure : EventDispatchResult.NotHandled;
                return _result;
            }
            finally {
                await idempotencyService.TryCompleteRequestAsync(
                    context.EventName,
                    $"{context.Source}:{evt.MessageId}",
                    _result == EventDispatchResult.Success,
                    cancellationToken: CancellationToken.None);
            }
        }
    }
}
