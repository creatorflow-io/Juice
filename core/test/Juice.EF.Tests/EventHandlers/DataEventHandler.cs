using System.Threading;
using System.Threading.Tasks;
using Juice.EF.Tests.Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.EF.Tests.EventHandlers
{
    public class DataEventHandler : INotificationHandler<DataEvent>
    {
        private readonly ILogger _logger;
        public DataEventHandler(ILogger<DataEventHandler> logger)
        {
            _logger = logger;
        }
        public Task Handle(DataEvent dataEvent, CancellationToken token)
        {
            _logger.LogInformation("DataEvent:" + JsonConvert.SerializeObject(dataEvent));
            if (dataEvent?.AuditRecord?.Entity is Content content)
            {
                _logger.LogInformation("Entity:" + content.Code);
            }
            return Task.CompletedTask;
        }
    }
}
