using System.Threading;
using System.Threading.Tasks;
using Juice.Domain.Events;
using Juice.MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Juice.EF.Tests.EventHandlers
{
    internal class AuditEventHandler<T> : INotificationHandler<T>
        where T : AuditEvent
    {
        private ILogger _logger;
        private SharedService _sharedService;
        public AuditEventHandler(ILogger<AuditEventHandler<T>> logger, SharedService sharedService)
        {
            _logger = logger;
            _sharedService = sharedService;
        }
        public ValueTask Handle(T notification, CancellationToken cancellationToken) {
            _logger.LogInformation("AuditEvent:" + typeof(T).Name + " " + JsonConvert.SerializeObject(notification.AuditRecord?.KeyValues));
            _sharedService.Handlers.Add(typeof(AuditEventHandler<T>).Name);
            return ValueTask.CompletedTask;
        }
    }
}
