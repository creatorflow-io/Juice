using Juice.EventBus;
using Juice.Messaging.Attributes;

namespace Juice.EF.Tests.Events
{
    [Domain("Contents")]
    public record ContentNameChangedIntegrationEvent : IntegrationEvent
    {
        public ContentNameChangedIntegrationEvent(Guid contentId,  string originalName, string name)
        {
            ContentId = contentId;
            OriginalName = originalName;
            Name = name;
        }
        public Guid ContentId { get; init; }
        public string Name { get; init; }
        public string OriginalName { get; init; }
    }
}
