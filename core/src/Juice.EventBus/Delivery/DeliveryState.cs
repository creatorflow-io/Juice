namespace Juice.EventBus.Delivery
{
    public enum DeliveryState
    {
        NotPublished = 0,
        InProgress = 1,
        Published = 2,
        Failed = 3,
        Skipped = 4 // e.g., when destination is not available, or the event is no longer relevant
    }
}
