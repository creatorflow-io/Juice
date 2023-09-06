using System.Threading;
using System.Threading.Tasks;
using Juice.Storage.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Storage.Tests.EventHandlers
{
    internal class FileUploadStartedEventHandler : INotificationHandler<FileUploadStartedEvent>
    {
        private ILogger _logger;
        public FileUploadStartedEventHandler(ILogger<FileUploadStartedEventHandler> logger)
        {
            _logger = logger;
        }
        public Task Handle(FileUploadStartedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"File upload started: {notification.Name}");
            return Task.CompletedTask;
        }
    }
}
