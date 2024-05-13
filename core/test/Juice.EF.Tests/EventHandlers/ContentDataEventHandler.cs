using System.Threading;
using System.Threading.Tasks;
using Juice.Domain;
using Juice.EF.Tests.Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.EF.Tests.EventHandlers
{
    public class ContentDataEventHandler : INotificationHandler<DataInserted<Content>>
    {
        private readonly ILogger _logger;
        public ContentDataEventHandler(ILogger<ContentDataEventHandler> logger)
        {
            _logger = logger;
        }
        public Task Handle(DataInserted<Content> dataEvent, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ContentDataEvent:" + JsonConvert.SerializeObject(dataEvent));
            return Task.CompletedTask;
        }
    }
}
