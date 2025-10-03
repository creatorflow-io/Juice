namespace Juice.MediatR
{
    public interface INotificationPipelineBehavior<TNotification> : IPipelineBehavior
     where TNotification : INotification
    {
        ValueTask Handle(
            TNotification notification,
            NotificationHandlerDelegate<TNotification> next,
            CancellationToken cancellationToken
            );
    }
}
