namespace Juice.Storage
{
    public interface IFileNameGenerator<T>
        where T : class, IFile, new()
    {
        Task<string> GenerateAsync(T file, CancellationToken token);
    }
}
