using Juice.Domain.Events;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.Events;
using Juice.EF.Tests.Infrastructure;
using Juice.MediatR;
using Juice.Messaging.Outbox;

namespace Juice.EF.Tests.EventHandlers
{
    internal class ContentInsertedEventHandler : INotificationHandler<DataInserted<Content>>
    {
        private readonly IOutboxService<TestContext> _integration;
        public ContentInsertedEventHandler(IOutboxService<TestContext> integration)
        {
            _integration = integration;
        }
        public async ValueTask Handle(DataInserted<Content> notification, CancellationToken cancellationToken = default)
        {
            await _integration.AddEventAsync(new ContentPublishedIntegrationEvent($"Content {notification.Entity!.Code} was published")
            {
                ContentId = notification.Entity!.Id,
                MessageId = notification.MessageId
            });
        }
    }
}
