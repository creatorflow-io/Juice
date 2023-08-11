using Juice.Storage.Abstractions;
using Newtonsoft.Json.Linq;

namespace Juice.Storage.Dto
{
    public record InitialFileInfo
    {
        public InitialFileInfo(string name, long fileSize, string? contentType
            , string? originalName, DateTimeOffset? lastModified
            , string? correlationId, JObject? metadata, FileExistsBehavior fileExists)
        {
            Name = name;
            FileSize = fileSize;
            ContentType = contentType;
            OriginalName = originalName;
            LastModified = lastModified;
            CorrelationId = correlationId;
            Metadata = metadata;
            FileExistsBehavior = fileExists;
        }

        public InitialFileInfo(string name, long fileSize, string? contentType
            , string? originalName, DateTimeOffset? lastModified
            , string? correlationId, JObject? metadata, FileExistsBehavior fileExists, Guid uploadId)
            : this(name, fileSize, contentType, originalName, lastModified, correlationId, metadata, fileExists)
        {
            UploadId = uploadId;
        }

        public Guid? UploadId { get; init; }
        public string Name { get; init; }
        public long FileSize { get; init; }
        public string? ContentType { get; init; }
        public string? CorrelationId { get; init; }
        public JObject? Metadata { get; init; }
        public string? OriginalName { get; init; }
        public DateTimeOffset? LastModified { get; init; }
        public FileExistsBehavior FileExistsBehavior { get; init; }
    }
}
