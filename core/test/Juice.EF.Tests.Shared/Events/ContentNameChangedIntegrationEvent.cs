using Juice.EventBus;

namespace Juice.EF.Tests.Events
{
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
