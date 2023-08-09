namespace Juice.Storage.Abstractions
{
    public interface IStorageFactory
    {
        IStorageProvider? CreateProvider(Protocol protocol, StorageEndpoint endpoint);
        IStorageProvider[] CreateProviders();
    }
}
