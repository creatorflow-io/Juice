using System.Reflection;
using System.Collections.Concurrent;
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
        /// Dispatches an <see cref="IIntegrationEvent"/> to all <c>IIntegrationEventHandler&lt;T&gt;</c>
        /// implementations discovered from <paramref name="serviceProvider"/>.
        /// Builds an <see cref="EventDispatchContext"/> from resolved handler types and delegates
        /// to <see cref="IntegrationEventDispatcher.DispatchAsync"/>.
        /// </summary>
        public static async Task<EventDispatchResult> DispatchIntegrationEventAsync(
            IServiceProvider serviceProvider,
            IntegrationEventDispatcher dispatcher,
            IIntegrationEvent evt,
            CancellationToken cancellationToken)
        {
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(evt.GetType());
            var handlerTypes = serviceProvider
                .GetServices(handlerType)
                .Select(h => h!.GetType())
                .ToList();

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
