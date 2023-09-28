namespace Juice.Storage
{
    public class UploadOptions
    {
        public long SectionSize { get; set; } = 10485760;

        public bool DeleteOnAbort { get; set; }
    }
}
