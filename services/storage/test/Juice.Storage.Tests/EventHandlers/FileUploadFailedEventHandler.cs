using System.Threading;
using System.Threading.Tasks;
using Juice.Storage.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Storage.Tests.EventHandlers
{
    internal class FileUploadFailedEventHandler : INotificationHandler<FileUploadFailedEvent>
    {
        private ILogger _logger;
        public FileUploadFailedEventHandler(ILogger<FileUploadFailedEventHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(FileUploadFailedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"File upload failed: {notification.Name}");
            return Task.CompletedTask;
        }
    }
}
