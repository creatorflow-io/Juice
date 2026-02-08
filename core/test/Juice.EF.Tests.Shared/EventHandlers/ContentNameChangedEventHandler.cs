using Juice.EF.Tests.Domain.Events;
using Juice.EF.Tests.Infrastructure;
using Juice.MediatR;
using Juice.Messaging.Outbox;

namespace Juice.EF.Tests.EventHandlers
{
    internal class ContentNameChangedEventHandler : INotificationHandler<ContentNameChangedEvent>
    {
        private readonly IOutboxService<TestContext> _integration;
        public ContentNameChangedEventHandler(IOutboxService<TestContext> integration)
        {
            _integration = integration;
        }
        public async ValueTask Handle(ContentNameChangedEvent notification, CancellationToken cancellationToken = default)
        {
            await _integration.AddEventAsync(notification);
        }
    }
}
