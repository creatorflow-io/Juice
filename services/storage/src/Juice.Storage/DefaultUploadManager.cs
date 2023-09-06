using System.Security.Claims;
using Juice.Storage.Abstractions;
using Juice.Storage.Authorization;
using Juice.Storage.Dto;
using Juice.Storage.Events;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.Storage
{
    internal class DefaultUploadManager<T> : IUploadManager
        where T : class, IFile, new()
    {
        public IStorage Storage => _storage;
        private readonly IStorageResolver _storageResolver;
        private readonly IStorage _storage;
        private readonly IUploadRepository<T> _uploadRepository;
        private readonly IFileRepository<T>? _fileRepository;

        private readonly IAuthorizationService? _authorizationService;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        private readonly IOptionsSnapshot<UploadOptions> _options;

        private readonly IMediator? _mediator;
        public DefaultUploadManager(
            IStorageResolver storageResolver,
            IStorage storage,
            IUploadRepository<T> uploadRepository,
            IOptionsSnapshot<UploadOptions> options,
            IHttpContextAccessor? httpContextAccessor = null,
            IFileRepository<T>? fileRepository = null,
            IMediator? mediator = default)
        {
            _storageResolver = storageResolver;
            _storage = storage;
            _uploadRepository = uploadRepository;
            _options = options;
            _fileRepository = fileRepository;
            _authorizationService = httpContextAccessor?.HttpContext?.RequestServices?.GetService<IAuthorizationService>();
            _httpContextAccessor = httpContextAccessor;
            _mediator = mediator;
        }

        public async Task CompleteAsync(Guid uploadId, CancellationToken token)
        {
            if (await _uploadRepository.ExistsAsync(_storageResolver.Identity, uploadId, token))
            {
                var file = await _uploadRepository.GetAsync(_storageResolver.Identity, uploadId, token);

                if (_fileRepository != null)
                {
                    await _fileRepository.AddAsync(file, _storageResolver.Identity, token);
                }
                if (_mediator != null)
                {
                    var username = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
                    await _mediator.Publish(new FileUploadedEvent(file.Id, file.Name, file.ContentType, file.PackageSize, file.CorrelationId, file.Metadata, username), token);
                }
                await _uploadRepository.RemoveAsync(_storageResolver.Identity, uploadId, token);
            }
        }

        public async Task FailureAsync(Guid uploadId, CancellationToken token)
        {
            if (await _uploadRepository.ExistsAsync(_storageResolver.Identity, uploadId, token))
            {
                var file = await _uploadRepository.GetAsync(_storageResolver.Identity, uploadId, token);

                await _storage.DeleteAsync(file.Name, token);

                await _uploadRepository.RemoveAsync(_storageResolver.Identity, uploadId, token);

                if (_mediator != null)
                {
                    var username = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
                    await _mediator.Publish(new FileUploadFailedEvent(file.Name, file.CorrelationId, username), token);
                }
            }
        }

        public async Task TimedoutAsync(Guid uploadId, CancellationToken token)
        {
            if (await _uploadRepository.ExistsAsync(_storageResolver.Identity, uploadId, token))
            {
                var file = await _uploadRepository.GetAsync(_storageResolver.Identity, uploadId, token);
                await _storage.DeleteAsync(file.Name, token);
                await _uploadRepository.RemoveAsync(_storageResolver.Identity, uploadId, token);
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
                if (!await _uploadRepository.ExistsAsync(_storageResolver.Identity, fileInfo.UploadId.Value, token))
                {
                    throw new ArgumentException("UploadId could not be found.");
                }

                var file = await _uploadRepository.GetAsync(_storageResolver.Identity, fileInfo.UploadId.Value, token);
                var fileName = file.Name;

                var exists = await _storage.ExistsAsync(fileName, token);
                if (!exists)
                {
                    throw new ArgumentException($"Uploading file {fileName} no longer exists.");
                }

                if (_mediator != null)
                {
                    var username = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
                    await _mediator.Publish(new FileUploadResumedEvent(file.Id, fileName, file.CorrelationId, username), token);
                }

                var size = await _storage.FileSizeAsync(fileName, token);
                return new UploadConfiguration(fileInfo.UploadId.Value, fileName, _options.Value.SectionSize, true, file.PackageSize, size);
            }
            else
            {
                var id = Guid.NewGuid();
                var file = new T()
                {
                    Id = id,
                    Name = fileInfo.Name,
                    PackageSize = fileInfo.FileSize,
                    ContentType = fileInfo.ContentType,
                    CorrelationId = fileInfo.CorrelationId,
                    Metadata = fileInfo.Metadata,
                    OriginalName = fileInfo.OriginalName,
                    LastModified = fileInfo.LastModified
                };


                if (_httpContextAccessor?.HttpContext != null && _authorizationService != null)
                {
                    var authorizationResult = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, file, StoragePolicies.CreateFile);
                    if (!authorizationResult.Succeeded)
                    {
                        throw new UnauthorizedAccessException("You are unauthorized to upload this file.");
                    }
                }
                var createdFileName = default(string);
                try
                {
                    createdFileName = await _storage.CreateAsync(fileInfo.Name, new CreateFileOptions { FileExistsBehavior = fileInfo.FileExistsBehavior }, token);

                    file.Name = createdFileName;
                    await _uploadRepository.AddAsync(_storageResolver.Identity, file);

                    if (_mediator != null)
                    {
                        var username = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
                        await _mediator.Publish(new FileUploadStartedEvent(file.Id, file.Name, file.ContentType, file.PackageSize, file.CorrelationId, file.Metadata, username), token);
                    }

                    return new UploadConfiguration(id, createdFileName, _options.Value.SectionSize, false, fileInfo.FileSize, 0);
                }
                catch (Exception)
                {
                    if (!string.IsNullOrEmpty(createdFileName))
                    {
                        await _storage.DeleteAsync(createdFileName, default);
                    }
                    throw;
                }
            }
        }

        public async Task<(bool Completed, long Size)> UploadAsync(Guid uploadId, Stream stream, long offset, CancellationToken token)
        {
            if (!await _uploadRepository.ExistsAsync(_storageResolver.Identity, uploadId, token))
            {
                throw new ArgumentException("UploadId could not be found.");
            }

            var file = await _uploadRepository.GetAsync(_storageResolver.Identity, uploadId, token);
            var fileName = file.Name;

            try
            {
                await _storage.WriteAsync(fileName, stream, offset, new TransferOptions(), token);
            }
            catch (OperationCanceledException)
            {
                if (_options.Value.DeleteOnAbort)
                {
                    await _storage.DeleteAsync(fileName, default); // token is already cancelled
                }
                await _uploadRepository.AbortAsync(_storageResolver.Identity, uploadId, _options.Value.DeleteOnAbort);
                throw;
            }
            var fileSize = await _storage.FileSizeAsync(fileName, token);
            if (fileSize == file.PackageSize)
            {
                await CompleteAsync(uploadId, token);
                return (true, fileSize);
            }
            return (false, fileSize);
        }
    }

}
