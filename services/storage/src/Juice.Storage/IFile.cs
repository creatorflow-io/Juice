using Newtonsoft.Json.Linq;

namespace Juice.Storage
{
    public interface IFile
    {
        public Guid Id { get; init; }
        public string Name { get; set; }
        public string OriginalName { get; init; }
        public long PackageSize { get; set; }
        public string ContentType { get; set; }
        public string? CorrelationId { get; set; }
        public JObject? Metadata { get; set; }
        public DateTimeOffset LastModified { get; set; }
    }
}
