namespace Juice.Messaging.Outbox.Delivery
{
    public static class DeliveryIntents
    {
        public const string SendPending = "send-pending";
        public const string RetryFailed = "retry-failed";
        public const string RecoverTimeout = "recover-timeout";
    }
}
