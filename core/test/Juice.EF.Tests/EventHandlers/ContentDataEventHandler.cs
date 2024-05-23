using System.Threading;
using System.Threading.Tasks;
using Juice.Domain.Events;
using Juice.EF.Tests.Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.EF.Tests.EventHandlers
{
    internal class ContentDataEventHandler : INotificationHandler<DataInserted<Content>>
    {
        private readonly ILogger _logger;
        private readonly SharedService _sharedService;
        public ContentDataEventHandler(ILogger<ContentDataEventHandler> logger, SharedService sharedService)
        {
            _logger = logger;
            _sharedService = sharedService;
        }
        public Task Handle(DataInserted<Content> dataEvent, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ContentDataEvent:" + JsonConvert.SerializeObject(dataEvent));
            _sharedService.Handlers.Add(typeof(ContentDataEventHandler).Name);
            return Task.CompletedTask;
        }
    }
}
