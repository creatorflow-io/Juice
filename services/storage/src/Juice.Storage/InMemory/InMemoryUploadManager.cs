using Juice.Storage.Abstractions;
using Juice.Storage.Dto;
using Microsoft.Extensions.Options;

namespace Juice.Storage.InMemory
{
    public class InMemoryUploadManager : IUploadManager
    {
        public IStorageProvider StorageProvider => _storageProvider;
        private readonly IStorageProvider _storageProvider;
        private readonly InMemoryStorageOptions _options;

        private static List<UploadFileInfo> _files = new List<UploadFileInfo>();
        public InMemoryUploadManager(InMemoryStorageFactory storageFactory, IOptionsSnapshot<InMemoryStorageOptions> options)
        {
            _storageProvider = storageFactory.CreateStorageProvider();
            _options = options.Value;
        }

        public Task CompleteAsync(Guid uploadId, CancellationToken token)
        {
            if (_files.Any(f => f.Id == uploadId))
            {
                var fileName = _files.RemoveAll(f => f.Id == uploadId);
            }
            return Task.CompletedTask;
        }

        public async Task FailureAsync(Guid uploadId, CancellationToken token)
        {
            //if (_idMap.ContainsKey(uploadId))
            //{
            //    var fileName = _idMap[uploadId];

            //    await _storageProvider.DeleteAsync(fileName, token);

            //    if (_fileNameMap.ContainsKey(fileName))
            //    {
            //        _fileNameMap.Remove(fileName);
            //    }
            //    _idMap.Remove(uploadId);
            //}
        }

        public Task<bool> ExistsAsync(string filePath, CancellationToken token)
        {
            return _storageProvider.ExistsAsync(filePath, token);
        }

        public async Task<UploadConfiguration> InitAsync(InitialFileInfo fileInfo, CancellationToken token)
        {
            if (fileInfo.FileExistsBehavior == FileExistsBehavior.Resume)
            {
                if (!fileInfo.UploadId.HasValue)
                {
                    throw new ArgumentException("UploadId must has value to resume upload process.");
                }
                if (!_files.Any(f => f.Id == fileInfo.UploadId.Value))
                {
                    throw new ArgumentException("UploadId could not be found.");
                }

                var file = _files.First(f => f.Id == fileInfo.UploadId.Value);
                var fileName = file.Name;

                var exists = await _storageProvider.ExistsAsync(fileName, token);
                if (!exists)
                {
                    throw new ArgumentException("Upload with specified id does not exist.");
                }

                var size = await _storageProvider.FileSizeAsync(fileName, token);
                return new UploadConfiguration(fileInfo.UploadId.Value, fileName, 10485760, true, file.PackageSize, size);
            }
            else
            {
                var createdFileName = await _storageProvider.CreateAsync(fileInfo.Name, new CreateFileOptions { FileExistsBehavior = fileInfo.FileExistsBehavior }, token);

                var id = Guid.NewGuid();
                _files.Add(new UploadFileInfo { Id = id, Name = createdFileName, PackageSize = fileInfo.FileSize, LastModified = DateTimeOffset.Now });
                return new UploadConfiguration(id, createdFileName, 10485760, false, fileInfo.FileSize, 0);
            }
        }

        public async Task<long> UploadAsync(Guid uploadId, Stream stream, long offset, CancellationToken token)
        {
            if (!_files.Any(f => f.Id == uploadId))
            {
                throw new Exception("Uploading file not found");
            }

            var file = _files.First(f => f.Id == uploadId);
            var fileName = file.Name;

            await _storageProvider.WriteAsync(fileName, stream, offset, new TransferOptions(), token);

            return await _storageProvider.FileSizeAsync(fileName, token);
        }
    }

    internal class UploadFileInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public long PackageSize { get; set; }
        public DateTimeOffset LastModified { get; set; }
    }
}
