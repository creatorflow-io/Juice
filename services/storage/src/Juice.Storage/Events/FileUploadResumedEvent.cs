using MediatR;

namespace Juice.Storage.Events
{
    public class FileUploadResumedEvent : INotification
    {
        public FileUploadResumedEvent
            (Guid id, string name, string? correlationId = default, string? userName = null)
        {
            Id = id;
            Name = name;
            CorrelationId = correlationId;
            UserName = userName;
        }
        public Guid Id { get; }
        public string Name { get; }
        public string? CorrelationId { get; }
        public string? UserName { get; set; }
    }
}
