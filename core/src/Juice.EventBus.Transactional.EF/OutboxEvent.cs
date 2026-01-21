using System.ComponentModel.DataAnnotations.Schema;
using Juice.Extensions;

namespace Juice.EventBus.Transactional.EF
{
    public class OutboxEvent
    {
        public Guid EventId { get; init; }
        public string EventTypeName { get; init; }
        [NotMapped]
        public string EventTypeShortName => EventTypeName.Split('.').Last();

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
