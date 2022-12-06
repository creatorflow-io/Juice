namespace Juice.Domain
{
    public interface IIdentifiable<TKey> where TKey : IEquatable<TKey>
    {
        TKey Id { get; }
        public string? Name { get; }
        public bool Disabled { get; }
    }
}
