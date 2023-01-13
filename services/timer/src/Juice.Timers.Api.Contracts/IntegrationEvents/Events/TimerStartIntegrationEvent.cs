using Juice.EventBus;

namespace Juice.Timers.Api.IntegrationEvents.Events
{
    public record TimerStartIntegrationEvent(string Issuer, string CorrelationId, DateTimeOffset AbsoluteExpired) : IntegrationEvent
    {
    }
}
