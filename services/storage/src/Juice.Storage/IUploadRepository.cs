namespace Juice.Storage
{
    public interface IUploadRepository<T>
        where T : class, IFile, new()
    {
        Task<bool> ExistsAsync(string storageIdentity, Guid uploadId, CancellationToken token);
        Task<T> GetAsync(string storageIdentity, Guid uploadId, CancellationToken token);
        Task RemoveAsync(string storageIdentity, Guid uploadId, CancellationToken token);
        Task AddAsync(string storageIdentity, T item);
        Task AbortAsync(string storageIdentity, Guid uploadId, bool fileDeleted);
        Task<IEnumerable<T>> FindAllBeforeAsync(string storageIdentity, DateTimeOffset date, CancellationToken token);
    }
}
