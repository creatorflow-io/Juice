using Juice.Storage.Abstractions;
using Juice.Storage.Dto;
using Microsoft.Extensions.Options;

namespace Juice.Storage
{
    internal class DefaultUploadManager<T> : IUploadManager
        where T : class, IFile, new()
    {
        private readonly IStorage _storage;
        private readonly IUploadRepository<T> _uploadRepository;
        private readonly IFileRepository<T>? _fileRepository;

        private readonly IOptionsSnapshot<UploadOptions> _options;
        public DefaultUploadManager(IStorage storage,
            IUploadRepository<T> uploadRepository,
            IOptionsSnapshot<UploadOptions> options,
            IFileRepository<T>? fileRepository = null)
        {
            _storage = storage;
            _uploadRepository = uploadRepository;
            _options = options;
            _fileRepository = fileRepository;
        }

        public async Task CompleteAsync(Guid uploadId, CancellationToken token)
        {
            if (await _uploadRepository.ExistsAsync(uploadId, token))
            {
                if (_fileRepository != null)
                {
                    var file = await _uploadRepository.GetAsync(uploadId, token);
                    await _fileRepository.AddAsync(file, token);
                }
                await _uploadRepository.RemoveAsync(uploadId, token);
            }
        }

        public async Task FailureAsync(Guid uploadId, CancellationToken token)
        {
            if (await _uploadRepository.ExistsAsync(uploadId, token))
            {
                var file = await _uploadRepository.GetAsync(uploadId, token);

                await _storage.DeleteAsync(file.Name, token);

                await _uploadRepository.RemoveAsync(uploadId, token);
            }
        }

        public Task<bool> ExistsAsync(string filePath, CancellationToken token)
        {
            return _storage.ExistsAsync(filePath, token);
        }

        public async Task<UploadConfiguration> InitAsync(InitialFileInfo fileInfo, CancellationToken token)
        {
            if (fileInfo.FileExistsBehavior == FileExistsBehavior.Resume)
            {
                if (!fileInfo.UploadId.HasValue)
                {
                    throw new ArgumentException("UploadId must has value to resume upload process.");
                }
                if (!await _uploadRepository.ExistsAsync(fileInfo.UploadId.Value, token))
                {
                    throw new ArgumentException("UploadId could not be found.");
                }

                var file = await _uploadRepository.GetAsync(fileInfo.UploadId.Value, token);
                var fileName = file.Name;

                var exists = await _storage.ExistsAsync(fileName, token);
                if (!exists)
                {
                    throw new ArgumentException($"Uploading file {fileName} no longer exists.");
                }

                var size = await _storage.FileSizeAsync(fileName, token);
                return new UploadConfiguration(fileInfo.UploadId.Value, fileName, _options.Value.SectionSize, true, file.PackageSize, size);
            }
            else
            {
                var createdFileName = await _storage.CreateAsync(fileInfo.Name, new CreateFileOptions { FileExistsBehavior = fileInfo.FileExistsBehavior }, token);

                var id = Guid.NewGuid();
                await _uploadRepository.AddAsync(new()
                {
                    Id = id,
                    Name = createdFileName,
                    PackageSize = fileInfo.FileSize,
                    ContentType = fileInfo.ContentType,
                    CorrelationId = fileInfo.CorrelationId,
                    Metadata = fileInfo.Metadata,
                    OriginalName = fileInfo.OriginalName,
                    LastModified = fileInfo.LastModified
                });
                return new UploadConfiguration(id, createdFileName, _options.Value.SectionSize, false, fileInfo.FileSize, 0);
            }
        }

        public async Task<long> UploadAsync(Guid uploadId, Stream stream, long offset, CancellationToken token)
        {
            if (!await _uploadRepository.ExistsAsync(uploadId, token))
            {
                throw new ArgumentException("UploadId could not be found.");
            }

            var file = await _uploadRepository.GetAsync(uploadId, token);
            var fileName = file.Name;

            await _storage.WriteAsync(fileName, stream, offset, new TransferOptions(), token);

            return await _storage.FileSizeAsync(fileName, token);
        }
    }

}
