namespace Juice.Messaging.Idempotency.EF
{
    public enum RequestState
    {
        New = 0,
        Processed = 1,
        Failed = 2
    }
}
