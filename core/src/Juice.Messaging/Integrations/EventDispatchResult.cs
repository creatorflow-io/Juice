
namespace Juice.Messaging.Integrations
{
    public enum EventDispatchResult
    {
        Failure,
        Success,
        NotHandled,
        /// <summary>Another dispatch of this message has already completed.</summary>
        Duplicated,
        /// <summary>
        /// Another dispatch of this message holds its idempotency guard, and may yet fail. Also reported when the idempotency
        /// store can't be reached, because then nothing is known. The handlers didn't run.
        /// </summary>
        InProgress
    }
}
