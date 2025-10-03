using System.Threading;
using System.Threading.Tasks;
using Juice.Domain.Events;
using Juice.MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.EF.Tests.EventHandlers
{
    internal class DataEventHandler<T> : INotificationHandler<T>
        where T : DataEvent
    {
        private readonly ILogger _logger;
        private readonly SharedService _sharedService;
        public DataEventHandler(ILogger<DataEventHandler<T>> logger, SharedService sharedService)
        {
            _logger = logger;
            _sharedService = sharedService;
        }
        public ValueTask Handle(T dataEvent, CancellationToken token)
        {
            if (dataEvent.IsAudit)
            {
                _logger.LogInformation("AuditEvent:" + typeof(T).Name + " " + JsonConvert.SerializeObject(dataEvent));
            }
            else
            {
                _logger.LogInformation("DataEvent:" + typeof(T).Name + " " + JsonConvert.SerializeObject(dataEvent));
            }

            _sharedService.Handlers.Add(typeof(DataEventHandler<T>).Name);
            return ValueTask.CompletedTask;
        }
    }
}
