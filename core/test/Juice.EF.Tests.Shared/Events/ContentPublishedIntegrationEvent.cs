using Juice.EventBus;
using Juice.Messaging.Attributes;

namespace Juice.EF.Tests.Events
{
    [Domain("Contents")]
    public record ContentPublishedIntegrationEvent : IntegrationEvent
    {
        public ContentPublishedIntegrationEvent(string message)
        {
            Message = message;
        }
        public Guid ContentId { get; init; }

        public string Message { get; init; }

    }
}
