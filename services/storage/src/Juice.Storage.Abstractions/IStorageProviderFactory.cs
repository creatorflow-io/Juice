namespace Juice.Storage.Abstractions
{
    public interface IStorageProviderFactory
    {
        IStorageProvider[] CreateProviders(IEnumerable<StorageEndpoint> endpoints);
    }
}
