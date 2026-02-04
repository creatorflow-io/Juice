using Juice.EventBus.Transactional;
using Juice.Extensions;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public class IntegrationEventLogEntry
    {
        public Guid EventId { get; private set; }
        public string EventTypeName { get; private set; }
        public EventState State { get; private set; }
        public int TimesSent { get; private set; }
        public DateTime? ProcessedOn { get; private set; }
        public string? LastError { get; private set; }

        public DateTime CreationTime { get; init; }
        public string Payload { get; init; }
        public string TransactionId { get; init; }

        public void UpdateState(EventState state, string? error = default)
        {
            if (state == EventState.InProgress)
            {
                TimesSent++;
            }
            LastError = error?.Truncate(LengthConstants.ShortDescriptionLength);
            State = state;
            ProcessedOn = DateTime.UtcNow;
        }
    }
}
