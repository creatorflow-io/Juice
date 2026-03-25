using System.Reflection;
using System.Collections.Concurrent;
using Juice.EventBus.Subscriptions;
using Juice.MediatR;
using Juice.Messaging.Integrations;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Local.Internal
{
    /// <summary>
    /// Shared static dispatch logic used by both <see cref="LocalChannelBackgroundService"/>
    /// and <see cref="LocalTransportPublisher"/> to invoke in-process handlers.
    /// </summary>
    internal static class LocalDispatchHelper
    {
        private static readonly ConcurrentDictionary<Type, MethodInfo> _publishMethodCache = new();

        /// <summary>
        /// Dispatches an <see cref="INotification"/> through the full MediatR notification
        /// pipeline via reflected <c>INotificationPublisher.Publish&lt;T&gt;</c>.
        /// </summary>
        public static async Task DispatchNotificationAsync(
            INotificationPublisher publisher,
            IMessage message,
            CancellationToken cancellationToken)
        {
            var method = _publishMethodCache.GetOrAdd(message.GetType(), t =>
                typeof(INotificationPublisher)
                    .GetMethod(nameof(INotificationPublisher.Publish))!
                    .MakeGenericMethod(t));

            await (ValueTask)method.Invoke(publisher, new object[] { message, cancellationToken })!;
        }

        /// <summary>
        /// Dispatches an <see cref="IIntegrationEvent"/> to handlers resolved via
        /// <paramref name="subscriptionsManager"/> when available, or via a DI container
        /// scan when <paramref name="subscriptionsManager"/> is <c>null</c> (backward-compatible
        /// fallback for callers that have not registered <c>AddLocalConsumer</c>).
        /// Builds an <see cref="EventDispatchContext"/> from resolved handler types and delegates
        /// to <see cref="IntegrationEventDispatcher.DispatchAsync"/>.
        /// </summary>
        /// <param name="serviceProvider">Scoped service provider for the current dispatch hop.</param>
        /// <param name="dispatcher">Integration event dispatcher.</param>
        /// <param name="subscriptionsManager">
        /// Optional keyed subscriptions manager (key <c>"local"</c>). When non-null, only handlers
        /// registered via <c>AddLocalConsumer</c> are invoked. When null, all
        /// <c>IIntegrationEventHandler&lt;T&gt;</c> services in the DI container are invoked.
        /// </param>
        /// <param name="evt">The integration event to dispatch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task<EventDispatchResult> DispatchIntegrationEventAsync(
            IServiceProvider serviceProvider,
            IntegrationEventDispatcher dispatcher,
            ISubscriptionsManager? subscriptionsManager,
            IIntegrationEvent evt,
            CancellationToken cancellationToken)
        {
            List<Type> handlerTypes;

            if (subscriptionsManager != null)
            {
                var types = await subscriptionsManager.GetHandlersForEventAsync(evt.GetType().Name);
                handlerTypes = types.ToList();
            }
            else
            {
                // Backward-compatible fallback: discover all handlers from DI.
                var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(evt.GetType());
                handlerTypes = serviceProvider
                    .GetServices(handlerType)
                    .Select(h => h!.GetType())
                    .ToList();
            }

            var source = MessageContext.IsInitialized
                ? MessageContext.Current.Source ?? string.Empty
                : string.Empty;
            var context = new EventDispatchContext(
                handlerTypes,
                evt.GetType().Name,
                evt.TenantId,
                source);

            return await dispatcher.DispatchAsync(evt, context);
        }
    }
}
