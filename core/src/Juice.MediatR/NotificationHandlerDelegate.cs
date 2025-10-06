using System.Runtime.CompilerServices;

namespace Juice.MediatR
{
    public readonly struct NotificationHandlerDelegate<TNotification>
    where TNotification : INotification
    {
        private readonly Func<TNotification, CancellationToken, ValueTask> _next;

        public NotificationHandlerDelegate(Func<TNotification, CancellationToken, ValueTask> next) => _next = next;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask Invoke(TNotification notification, CancellationToken ct) => _next(notification, ct);
    }
}
