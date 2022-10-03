using Juice.Storage.Abstractions;

namespace Juice.Storage.Dto
{
    public record InitialFileInfo
    {
        public InitialFileInfo(string name, long fileSize, FileExistsBehavior fileExists)
        {
            Name = name;
            FileSize = fileSize;
            FileExistsBehavior = fileExists;
        }

        public InitialFileInfo(string name, long fileSize, FileExistsBehavior fileExists, Guid uploadId)
            : this(name, fileSize, fileExists)
        {
            UploadId = uploadId;
        }

        public Guid? UploadId { get; init; }
        public string Name { get; init; }
        public long FileSize { get; init; }
        public FileExistsBehavior FileExistsBehavior { get; init; }
    }
}
