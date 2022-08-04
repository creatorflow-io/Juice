using System.Net;

namespace Juice.Storage.Abstractions
{
    public interface IStorage : IDisposable
    {
        StorageEndpoint? StorageEndpoint { get; }
        NetworkCredential? Credential { get; }

        /// <summary>
        /// Configure credential to authenticate
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        IStorage WithCredential(NetworkCredential credential);

        /// <summary>
        /// Configure endpoint info and init connection if needed
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        IStorage Configure(StorageEndpoint endpoint);

        /// <summary>
        /// Open stream to read file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<Stream> ReadAsync(string filePath, CancellationToken token);

        /// <summary>
        /// Write input stream to store file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="stream"></param>
        /// <param name="offset"></param>
        /// <param name="options"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task WriteAsync(string filePath, Stream stream, long offset, TransferOptions options, CancellationToken token);

        /// <summary>
        /// Create new file on storage
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="options"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<string> CreateAsync(string filePath, CreateFileOptions options, CancellationToken token);

        /// <summary>
        /// Check if file is existing on storage
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(string filePath, CancellationToken token);

        /// <summary>
        /// Get size of file on storage
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<long> FileSizeAsync(string filePath, CancellationToken token);

        /// <summary>
        /// Delete file if exists
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task DeleteAsync(string filePath, CancellationToken token);
    }
}
