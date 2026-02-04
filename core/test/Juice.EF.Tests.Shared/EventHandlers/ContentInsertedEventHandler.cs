using Juice.Domain.Events;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.Events;
using Juice.EF.Tests.Infrastructure;
using Juice.EventBus.Transactional;
using Juice.MediatR;

namespace Juice.EF.Tests.EventHandlers
{
    internal class ContentInsertedEventHandler : INotificationHandler<DataInserted<Content>>
    {
        private readonly IIntegrationEventService<TestContext> _integration;
        public ContentInsertedEventHandler(IIntegrationEventService<TestContext> integration)
        {
            _integration = integration;
        }
        public async ValueTask Handle(DataInserted<Content> notification, CancellationToken cancellationToken = default)
        {
            await _integration.AddEventAsync(new ContentPublishedIntegrationEvent($"Content {notification.Entity!.Code} was published")
            {
                ContentId = notification.Entity!.Id,
                Id = notification.EventId
            });
        }
    }
}
