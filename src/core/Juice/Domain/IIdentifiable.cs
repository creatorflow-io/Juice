namespace Juice.Domain
{
    public interface IIdentifiable<TKey> where TKey : IEquatable<TKey>
    {
        TKey Id { get; }
    }
}
