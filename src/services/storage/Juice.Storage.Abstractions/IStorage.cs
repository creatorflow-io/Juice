using System.Net;

namespace Juice.Storage.Abstractions
{
    public interface IStorage : IDisposable
    {
        StorageEndpoint? StorageEndpoint { get; }
        NetworkCredential? Credential { get; }
        IStorage WithCredential(NetworkCredential credential);
        IStorage Configure(StorageEndpoint endpoint);
        Task<Stream> ReadAsync(string filePath, CancellationToken token);
        Task WriteAsync(string filePath, Stream stream, long offset, TransferOptions options, CancellationToken token);
        Task<string> CreateAsync(string filePath, CreateFileOptions options, CancellationToken token);
        Task<bool> ExistsAsync(string filePath, CancellationToken token);
        Task<long> FileSizeAsync(string filePath, CancellationToken token);
        Task DeleteAsync(string filePath, CancellationToken token);
    }
}
