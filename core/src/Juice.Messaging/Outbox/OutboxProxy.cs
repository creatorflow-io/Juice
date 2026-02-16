using System.Reflection;

namespace Juice.Messaging.Outbox
{

    internal class OutboxProxy<T> : DispatchProxy
    {
        private IOutboxService? _outbox;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new ArgumentNullException(nameof(targetMethod), "Target method cannot be null.");
            }
            if (typeof(IOutboxService).GetMethod(targetMethod.Name) == null)
            {
                throw new InvalidOperationException($"Method {targetMethod.Name} is not a valid IEventBus method.");
            }
            return targetMethod?.Invoke(_outbox, args);
        }

        public static T Create(IOutboxService outbox)
        {
            object? proxy = Create<T, OutboxProxy<T>>();
            if (proxy is null)
            {
                throw new InvalidOperationException("Failed to create EventBusProxy instance.");
            }
            ((OutboxProxy<T>)proxy)._outbox = outbox;
            return (T)proxy;
        }
    }
}
