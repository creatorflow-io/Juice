namespace Juice.Storage
{
    public interface IFileRepository<T>
        where T : class, IFile, new()
    {
        Task AddAsync(T item, CancellationToken token);
    }
}
