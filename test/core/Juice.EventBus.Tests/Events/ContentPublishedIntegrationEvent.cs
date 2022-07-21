namespace Juice.EventBus.Tests.Events
{
    public record ContentPublishedIntegrationEvent : IntegrationEvent
    {
        public ContentPublishedIntegrationEvent(string message)
            : base()
        {
            Message = message;
        }
        public string Message { get; set; }
    }
}
