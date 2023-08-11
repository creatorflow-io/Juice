using Juice.Storage.Abstractions;
using Newtonsoft.Json.Linq;

namespace Juice.Storage.Dto
{
    public record InitialFileInfo
    {
        public InitialFileInfo(string name, long fileSize, string? contentType
            , string? correlationId, JObject? metadata, FileExistsBehavior fileExists)
        {
            Name = name;
            FileSize = fileSize;
            ContentType = contentType;
            CorrelationId = correlationId;
            Metadata = metadata;
            FileExistsBehavior = fileExists;
        }

        public InitialFileInfo(string name, long fileSize, string? contentType
            , string? correlationId, JObject? metadata, FileExistsBehavior fileExists, Guid uploadId)
            : this(name, fileSize, contentType, correlationId, metadata, fileExists)
        {
            UploadId = uploadId;
        }

        public Guid? UploadId { get; init; }
        public string Name { get; init; }
        public long FileSize { get; init; }
        public string? ContentType { get; init; }
        public string? CorrelationId { get; init; }
        public JObject? Metadata { get; init; }
        public FileExistsBehavior FileExistsBehavior { get; init; }
    }
}
