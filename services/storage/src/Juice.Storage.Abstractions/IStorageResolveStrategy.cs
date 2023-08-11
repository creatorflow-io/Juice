namespace Juice.Storage.Abstractions
{
    public interface IStorageResolveStrategy : IStorageResolver
    {
        int Priority { get; }
    }
}
