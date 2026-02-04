using Juice.EF.Tests.Domain.Events;
using Juice.EF.Tests.Events;
using Juice.EF.Tests.Infrastructure;
using Juice.EventBus.Transactional;
using Juice.MediatR;

namespace Juice.EF.Tests.EventHandlers
{
    internal class ContentNameChangedEventHandler : INotificationHandler<ContentNameChangedEvent>
    {
        private readonly IIntegrationEventService<TestContext> _integration;
        public ContentNameChangedEventHandler(IIntegrationEventService<TestContext> integration)
        {
            _integration = integration;
        }
        public async ValueTask Handle(ContentNameChangedEvent notification, CancellationToken cancellationToken = default)
        {
            await _integration.AddEventAsync(new ContentNameChangedIntegrationEvent(notification.ContentId,
                notification.OriginalName, notification.Name));
        }
    }
}
