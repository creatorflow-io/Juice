namespace Juice.Storage
{
    public interface IUploadRepository<T>
        where T : class, IFile, new()
    {
        Task<bool> ExistsAsync(Guid uploadId, CancellationToken token);
        Task<T> GetAsync(Guid uploadId, CancellationToken token);
        Task RemoveAsync(Guid uploadId, CancellationToken token);
        Task AddAsync(T item);
        Task AbortAsync(Guid uploadId, bool fileDeleted);
    }
}
