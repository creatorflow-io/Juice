using System.Reflection;

namespace Juice.EventBus
{
    [Obsolete("This class is deprecated. Use EventBusProxy instead.")]
    public abstract class EventBusWrapper : IEventBus
    {
        private readonly IEventBus _eventBus;
        public EventBusWrapper(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }
        public Task PublishAsync(IntegrationEvent @event, string? tenantId = null)
            => _eventBus.PublishAsync(@event, tenantId);

        public void Subscribe<T, TH>(string? key = null)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
            => _eventBus.Subscribe<T, TH>(key);

        public void Unsubscribe<T, TH>(string? key = null)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
            => _eventBus.Unsubscribe<T, TH>(key);
    }

    internal class EventBusProxy<T> : DispatchProxy
    {
        private IEventBus _eventBus;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new ArgumentNullException(nameof(targetMethod), "Target method cannot be null.");
            }
            if (typeof(IEventBus).GetMethod(targetMethod.Name)==null)
            {
                throw new InvalidOperationException($"Method {targetMethod.Name} is not a valid IEventBus method.");
            }
            return targetMethod?.Invoke(_eventBus, args);
        }

        public static T Create(IEventBus eventBus)
        {
            object? proxy = Create<T, EventBusProxy<T>>();
            if (proxy is null)
            {
                throw new InvalidOperationException("Failed to create EventBusProxy instance.");
            }
            ((EventBusProxy<T>)proxy)._eventBus = eventBus;
            return (T)proxy;
        }
    }
}
