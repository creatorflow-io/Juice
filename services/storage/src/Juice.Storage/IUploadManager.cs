using Juice.Storage.Abstractions;
using Juice.Storage.Dto;

namespace Juice.Storage
{
    public interface IUploadManager
    {
        IStorageProvider StorageProvider { get; }
        Task<bool> ExistsAsync(string filePath, CancellationToken token);
        Task<UploadConfiguration> InitAsync(InitialFileInfo fileInfo, CancellationToken token);
        Task<long> UploadAsync(Guid uploadId, Stream stream, long offset, CancellationToken token);
        Task CompleteAsync(Guid uploadId, CancellationToken token);
        Task FailureAsync(Guid uploadId, CancellationToken token);
    }
}
