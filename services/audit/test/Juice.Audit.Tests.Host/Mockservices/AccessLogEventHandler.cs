using Juice.Audit.Domain.Events;
using MediatR;

namespace Juice.Audit.Tests.Host.Mockservices
{
    public class AccessLogCreatedDomainEventHandler
        : INotificationHandler<AccessLogCreatedDomainEvent>
    {
        private readonly ILogger _logger;
        public AccessLogCreatedDomainEventHandler(ILogger<AccessLogCreatedDomainEventHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(AccessLogCreatedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("AccessLogCreatedDomainEvent was handled! {0}", notification.RecordId);
            return Task.CompletedTask;
        }
    }
}
