using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace Juice.Storage.InMemory
{
    internal class InMemoryUploadRepository : IUploadRepository<UploadFileInfo>
    {
        private readonly ConcurrentDictionary<Guid, UploadFileInfo> _uploads
            = new ConcurrentDictionary<Guid, UploadFileInfo>();

        public Task AbortAsync(Guid uploadId, bool fileDeleted)
        {
            return Task.CompletedTask;
        }

        public Task AddAsync(UploadFileInfo item)
        {
            _uploads.TryAdd(item.Id, item);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid uploadId, CancellationToken token)
            => Task.FromResult(_uploads.ContainsKey(uploadId));
        public Task<UploadFileInfo> GetAsync(Guid uploadId, CancellationToken token)
            => Task.FromResult(_uploads[uploadId]);

        public Task RemoveAsync(Guid uploadId, CancellationToken token)
            => Task.FromResult(_uploads.TryRemove(uploadId, out _));
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
    }
}
