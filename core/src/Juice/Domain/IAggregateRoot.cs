namespace Juice.Domain
{
    public interface IAggregateRoot<TNotification>
    {
        IList<TNotification> DomainEvents { get; }
    }

    public static class AggregateRootExtensions
    {
        public static void AddDomainEvent<T>(this IAggregateRoot<T> aggregrate, T @event)
        {
            aggregrate.DomainEvents.Add(@event);
        }

        public static void ClearDomainEvents<T>(this IAggregateRoot<T> aggregrate)
        {
            aggregrate.DomainEvents.Clear();
        }

    }
}
