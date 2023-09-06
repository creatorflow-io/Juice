using MediatR;
using Newtonsoft.Json.Linq;

namespace Juice.Storage.Events
{
    public class FileUploadStartedEvent : INotification
    {
        public FileUploadStartedEvent(Guid id, string name, string? contentType, long length, string? correlationId = default, JObject? metadata = default, string? userName = null)
        {
            Id = id;
            Name = name;
            ContentType = contentType;
            Length = length;
            CorrelationId = correlationId;
            Metadata = metadata;
            UserName = userName;
        }
        public Guid Id { get; }
        public string Name { get; }
        public string? ContentType { get; }
        public long Length { get; }
        public string? CorrelationId { get; }
        public JObject? Metadata { get; }
        public string? UserName { get; }
    }
}
