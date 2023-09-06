using System.Threading;
using System.Threading.Tasks;
using Juice.Storage.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Juice.Storage.Tests.EventHandlers
{
    internal class FileUploadResumedEventHandler : INotificationHandler<FileUploadResumedEvent>
    {
        private ILogger _logger;
        public FileUploadResumedEventHandler(ILogger<FileUploadResumedEventHandler> logger)
        {
            _logger = logger;
        }
        public Task Handle(FileUploadResumedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"File upload resumed: {notification.Name}");
            return Task.CompletedTask;
        }
    }
}
