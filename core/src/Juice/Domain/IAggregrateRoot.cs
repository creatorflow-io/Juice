namespace Juice.Domain
{
    public interface IAggregrateRoot<TNotification>
    {
        IList<TNotification> DomainEvents { get; }
    }

    public static class AggregrateRootExtensions
    {
        public static void AddDomainEvent<T>(this IAggregrateRoot<T> aggregrate, T @event)
        {
            aggregrate.DomainEvents.Add(@event);
        }

        public static void ClearDomainEvents<T>(this IAggregrateRoot<T> aggregrate)
        {
            aggregrate.DomainEvents.Clear();
        }

    }
}
