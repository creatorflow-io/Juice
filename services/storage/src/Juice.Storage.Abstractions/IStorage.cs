﻿namespace Juice.Storage.Abstractions
{
    /// <summary>
    /// Implementation of this interface should be able to connect to storage for read/write file operations
    /// via implementation of IStorageProvider and not depends on any protocol
    /// </summary>
    public interface IStorage : IDisposable
    {
        /// <summary>
        /// Supported protocols
        /// </summary>
        Protocol[] Protocols { get; }

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
