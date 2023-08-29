namespace Juice.Storage
{
    public class UploadOptions
    {
        public long SectionSize { get; set; } = 10485760 * 10;
        public bool DeleteOnAbort { get; set; }
    }
}
