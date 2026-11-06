namespace Juice.EventBus.Publishing
{
    /// <summary>
    /// Thrown by an <see cref="ITransportPublisher"/> when the message can't be settled yet, without anything having gone wrong.
    /// Outbox delivery tries it again later and doesn't count a failure or use up a retry.
    /// </summary>
    public sealed class DeliveryDeferredException : Exception
    {
        public DeliveryDeferredException(string message) : base(message)
        {
        }
    }
}
