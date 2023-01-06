namespace Juice.Domain
{
    public interface IAggregrateRoot<TNotification>
    {
        IList<TNotification> Notifications { get; }
    }

    public static class AggregrateRootExtensions
    {
        public static void AddDomainEvent<T>(this IAggregrateRoot<T> aggregrate, T notification)
        {
            aggregrate.Notifications.Add(notification);
        }
    }
}
