namespace Juice.Storage.Abstractions
{
    /// <summary>
    /// Store storage endpoints
    /// </summary>
    public interface IStorageRepository
    {
        Task<bool> ExistsAsync(string identity);
        Task<IEnumerable<StorageEndpoint>> GetEndpointsAsync(string identity);
    }
}
