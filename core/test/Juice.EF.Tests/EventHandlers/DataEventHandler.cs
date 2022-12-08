using System.Threading.Tasks;
using Juice.EF.Tests.Domain;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.EF.Tests.EventHandlers
{
    public class DataEventHandler : IDataEventHandler
    {
        private readonly ILogger _logger;
        public DataEventHandler(ILogger<DataEventHandler> logger)
        {
            _logger = logger;
        }
        public async Task HandleAsync(DataEvent dataEvent)
        {
            _logger.LogInformation("DataEvent:" + JsonConvert.SerializeObject(dataEvent));
            if (dataEvent?.AuditRecord?.Entity is Content content)
            {
                _logger.LogInformation("Entity:" + content.Code);
            }
        }
    }
}
