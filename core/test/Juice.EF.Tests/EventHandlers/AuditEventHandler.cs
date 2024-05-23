using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Juice.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

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
        public Task Handle(T notification, CancellationToken cancellationToken) {
            _logger.LogInformation("AuditEvent:" + typeof(T).Name + " " + notification);
            _sharedService.Handlers.Add(typeof(AuditEventHandler<T>).Name);
            return Task.CompletedTask;
        }
    }
}
