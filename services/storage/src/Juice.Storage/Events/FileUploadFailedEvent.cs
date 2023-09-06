using MediatR;

namespace Juice.Storage.Events
{
    public class FileUploadFailedEvent : INotification
    {
        public FileUploadFailedEvent(string name, string? correlationId, string? userName)
        {
            Name = name;
            CorrelationId = correlationId;
            UserName = userName;
        }
        public string Name { get; }
        public string? CorrelationId { get; }
        public string? UserName { get; set; }
    }
}
