using Juice.Extensions;

namespace Juice.Messaging.Outbox
{
    public class OutboxDelivery
    {
        public Guid DeliveryId { get; init; } = Guid.NewGuid();
        public Guid EventId { get; init; }
        public string PublisherKey { get; init; } = default!;
        public string Destination { get; init; } = default!;
        public DateTimeOffset CreationTime { get; init; }
        public DeliveryState State { get; private set; }
        public int RetryCount { get; private set; }
        public DateTimeOffset? ProcessedOn { get; private set; }
        public string? LastError { get; private set; }
        public DateTimeOffset? NextAttemptOn { get; private set; }

        public virtual OutboxEvent OutboxEvent { get; init; } = default!;

        public void UpdateState(DeliveryState state, string? error = default, DateTimeOffset? nextAttempt = default)
        {
            if (State == DeliveryState.Failed && state == DeliveryState.InProgress)
            {
                RetryCount++;
            }
            LastError = error?.Truncate(LengthConstants.ShortDescriptionLength);
            NextAttemptOn = nextAttempt;
            State = state;
            ProcessedOn = DateTime.UtcNow;
        }
    }
}
