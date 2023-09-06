using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace Juice.Storage.InMemory
{
    internal class InMemoryUploadRepository : IUploadRepository<UploadFileInfo>
    {
        private readonly ConcurrentDictionary<string, List<UploadFileInfo>> _uploads
            = new ConcurrentDictionary<string, List<UploadFileInfo>>();

        public Task AbortAsync(string storageIdentity, Guid uploadId, bool fileDeleted)
        {
            return Task.CompletedTask;
        }

        public Task AddAsync(string storageIdentity, UploadFileInfo item)
        {
            if (storageIdentity == null) { throw new ArgumentNullException(nameof(storageIdentity)); }
            item.StartedTime = DateTimeOffset.Now;
            if (!_uploads.ContainsKey(storageIdentity))
            {
                _uploads.TryAdd(storageIdentity, new List<UploadFileInfo>());
            }
            _uploads[storageIdentity].Add(item);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string storageIdentity, Guid uploadId, CancellationToken token)
        {
            if (storageIdentity == null) { throw new ArgumentNullException(nameof(storageIdentity)); }
            if (uploadId == Guid.Empty) { throw new ArgumentNullException(nameof(uploadId)); }
            return Task.FromResult(_uploads.ContainsKey(storageIdentity) && _uploads[storageIdentity].Any(u => u.Id == uploadId));
        }

        public Task<IEnumerable<UploadFileInfo>> FindAllBeforeAsync(string storageIdentity, DateTimeOffset date, CancellationToken token)
        {
            if (storageIdentity == null) { throw new ArgumentNullException(nameof(storageIdentity)); }
            return Task.FromResult(_uploads.ContainsKey(storageIdentity)
                ? _uploads[storageIdentity].Where(u => u.StartedTime < date)
                    .ToArray().AsEnumerable() // to avoid "Collection was modified; enumeration operation may not execute." error
                : Array.Empty<UploadFileInfo>());
        }

        public Task<UploadFileInfo> GetAsync(string storageIdentity, Guid uploadId, CancellationToken token)
        {
            if (storageIdentity == null) { throw new ArgumentNullException(nameof(storageIdentity)); }
            if (uploadId == Guid.Empty) { throw new ArgumentNullException(nameof(uploadId)); }
            if (_uploads.ContainsKey(storageIdentity))
            {
                return Task.FromResult(_uploads[storageIdentity].First(u => u.Id == uploadId));
            }
            throw new KeyNotFoundException();
        }

        public Task RemoveAsync(string storageIdentity, Guid uploadId, CancellationToken token)
        {
            if (storageIdentity == null) { throw new ArgumentNullException(nameof(storageIdentity)); }
            if (uploadId == Guid.Empty) { throw new ArgumentNullException(nameof(uploadId)); }
            if (_uploads.ContainsKey(storageIdentity))
            {
                _uploads[storageIdentity].RemoveAll(u => u.Id == uploadId);
            }
            return Task.CompletedTask;
        }
    }

    internal class UploadFileInfo : IFile
    {
        public Guid Id { get; init; }
        public string Name { get; set; }
        public long PackageSize { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public string? ContentType { get; set; }
        public string? OriginalName { get; init; }
        public string? CorrelationId { get; set; }
        public JObject? Metadata { get; set; }
        public DateTimeOffset StartedTime { get; set; }
    }
}
