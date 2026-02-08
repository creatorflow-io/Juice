namespace Juice.MediatR
{
    /// <summary>
    /// Marker interface for notifications, default interface for parallel notifications.
    /// </summary>
    public interface INotification: IMessage
    {
    }

    /// <summary>
    /// Marker interface for sequence notifications, notifications that should be handled in order.
    /// </summary>
    public interface ISequenceNotification : INotification
    {
    }

    /// <summary>
    /// Marker interface for fire-and-forget notifications, notifications that do not require a response and can be handled in the background.
    /// </summary>

    public interface IFireAndForgetNotification : INotification
    {
    }
}
