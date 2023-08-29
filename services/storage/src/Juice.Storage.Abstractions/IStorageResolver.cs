namespace Juice.Storage.Abstractions
{
    /// <summary>
    /// Resolve storage endpoint from request
    /// </summary>
    public interface IStorageResolver : IDisposable
    {
        bool IsResolved { get; }
        string Identity { get; }
        IStorage? Storage { get; }
        IEnumerable<StorageEndpoint> Endpoints { get; }
        Task<bool> TryResolveAsync(string identity);
    }
}

