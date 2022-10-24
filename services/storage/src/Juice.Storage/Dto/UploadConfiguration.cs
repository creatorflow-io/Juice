namespace Juice.Storage.Dto
{
    public record UploadConfiguration
    {
        public UploadConfiguration(Guid id, string name, long sectionSize, bool exists, long packageSize, long offset)
        {
            UploadId = id;
            Name = name;
            SectionSize = sectionSize;
            Exists = exists;
            PackageSize = packageSize;
            Offset = offset;
        }
        public Guid UploadId { get; init; }
        public string Name { get; init; }
        public long SectionSize { get; init; }
        public bool Exists { get; init; }
        public long PackageSize { get; init; }
        public long Offset { get; init; }
    }
}
