using System;

namespace Juice.EventBus.Tests.Events
{
    public record ContentPublishedIntegrationEvent : IntegrationEvent, IMultiTenantIntegrationEvent
    {
        public ContentPublishedIntegrationEvent(string message)
        {
            Message = message;
        }
        public Guid ContentId { get; init; }

        public string Message { get; init; }

        public string? TenantId { get; init; }
    }
}
